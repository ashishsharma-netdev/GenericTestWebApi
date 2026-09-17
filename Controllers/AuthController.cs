using System.Security.Claims;
using Google.Apis.Auth;
using GenericTestWebApi.Auth;
using GenericTestWebApi.Data;
using GenericTestWebApi.Entities;
using GenericTestWebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(TestPrepDbContext db, JwtTokenService tokens, IConfiguration configuration, SubscriptionLifecycleService lifecycle) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var fullName = request.FullName?.Trim() ?? string.Empty;
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;

        if (fullName.Length < 2 || fullName.Length > 150)
            return BadRequest(new { message = "Full name must be between 2 and 150 characters." });
        if (!IsValidEmail(email))
            return BadRequest(new { message = "Please enter a valid email address." });
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return BadRequest(new { message = "Password must contain at least 6 characters." });

        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
            return Conflict(new { message = "Email is already registered." });

        var role = await db.Roles.SingleOrDefaultAsync(x => x.Name == "FreeUser", cancellationToken);
        if (role is null)
            return StatusCode(500, new { message = "Student account role is not configured. Please contact support." });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = new UserEntity { FullName = fullName, Email = email, IsActive = true };
            user.PasswordHash = new PasswordHasher<UserEntity>().HashPassword(user, request.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);

            db.UserRoles.Add(new UserRoleEntity { UserId = user.Id, RoleId = role.Id });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            user.UserRoles = new List<UserRoleEntity> { new() { UserId = user.Id, RoleId = role.Id, Role = role } };
            return Ok(await IssueTokens(user));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null || !user.IsActive || new PasswordHasher<UserEntity>().VerifyHashedPassword(user, user.PasswordHash, request.Password ?? string.Empty) != PasswordVerificationResult.Success)
            return Unauthorized(new { message = "Invalid email or password." });

        await lifecycle.ExpireUserSubscriptionsAsync(user.Id, cancellationToken);
        user.UserRoles = await db.UserRoles.Where(x => x.UserId == user.Id).Include(x => x.Role).ToListAsync(cancellationToken);
        return Ok(await IssueTokens(user));
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken)) return BadRequest(new { message = "Google ID token is required." });
        var clientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId)) return StatusCode(503, new { message = "Google Sign-In is not configured on the server." });

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { clientId } });
        }
        catch (InvalidJwtException)
        {
            return Unauthorized(new { message = "Google ID token is invalid or expired." });
        }

        if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
            return Unauthorized(new { message = "Google account email could not be verified." });

        var email = payload.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.GoogleSubject == payload.Subject, cancellationToken);
        if (user is null)
        {
            user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
            if (user is not null)
            {
                if (!string.IsNullOrWhiteSpace(user.GoogleSubject) && user.GoogleSubject != payload.Subject)
                    return Conflict(new { message = "This email is already linked to another Google account." });
                user.GoogleSubject = payload.Subject;
                if (string.IsNullOrWhiteSpace(user.FullName)) user.FullName = payload.Name ?? email.Split('@')[0];
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                user = new UserEntity
                {
                    FullName = string.IsNullOrWhiteSpace(payload.Name) ? email.Split('@')[0] : payload.Name,
                    Email = email,
                    GoogleSubject = payload.Subject,
                    IsActive = true
                };
                user.PasswordHash = new PasswordHasher<UserEntity>().HashPassword(user, Guid.NewGuid().ToString("N"));
                db.Users.Add(user);
                await db.SaveChangesAsync(cancellationToken);
                var role = await db.Roles.SingleAsync(x => x.Name == "FreeUser", cancellationToken);
                db.UserRoles.Add(new UserRoleEntity { UserId = user.Id, RoleId = role.Id });
                await db.SaveChangesAsync(cancellationToken);
                user.UserRoles = await db.UserRoles.Where(x => x.UserId == user.Id).Include(x => x.Role).ToListAsync(cancellationToken);
            }
        }

        if (!user.IsActive) return Unauthorized(new { message = "This account is inactive." });
        await lifecycle.ExpireUserSubscriptionsAsync(user.Id, cancellationToken);
        user.UserRoles = await db.UserRoles.Where(x => x.UserId == user.Id).Include(x => x.Role).ToListAsync(cancellationToken);
        return Ok(await IssueTokens(user));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return Unauthorized(new { message = "Refresh token is required." });
        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var token = await db.RefreshTokens.Include(x => x.User).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow, cancellationToken);
        if (token is null || !token.User.IsActive) return Unauthorized(new { message = "Refresh token is invalid or expired." });

        await lifecycle.ExpireUserSubscriptionsAsync(token.User.Id, cancellationToken);
        token.User.UserRoles = await db.UserRoles.Where(x => x.UserId == token.User.Id).Include(x => x.Role).ToListAsync(cancellationToken);
        token.RevokedAtUtc = DateTime.UtcNow;
        return Ok(await IssueTokens(token.User));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = tokens.HashRefreshToken(request.RefreshToken);
            var token = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash);
            if (token != null) { token.RevokedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(); }
        }
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (user is null || !user.IsActive) return Unauthorized();
        return Ok(new { id = user.Id, fullName = user.FullName, email = user.Email, roles = user.UserRoles.Select(x => x.Role.Name), createdAtUtc = user.CreatedAtUtc });
    }

    private async Task<object> IssueTokens(UserEntity user)
    {
        var roles = user.UserRoles.Select(x => x.Role.Name).ToList();
        if (roles.Count == 0) roles = await db.UserRoles.Where(x => x.UserId == user.Id).Include(x => x.Role).Select(x => x.Role.Name).ToListAsync();
        var access = tokens.CreateAccessToken(user, roles);
        var refresh = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshTokenEntity { UserId = user.Id, TokenHash = refresh.Hash, ExpiresAtUtc = refresh.ExpiresAtUtc });
        await db.SaveChangesAsync();
        return new { accessToken = access, refreshToken = refresh.RawToken, expiresAtUtc = DateTime.UtcNow.AddMinutes(60), user = new { id = user.Id, fullName = user.FullName, email = user.Email, roles } };
    }

    private static bool IsValidEmail(string email) => email.Length <= 254 && email.Contains('@') && email.IndexOf('@') > 0 && email.LastIndexOf('.') > email.IndexOf('@') + 1 && !email.EndsWith('.');

    public record RegisterRequest(string FullName, string Email, string Password);
    public record LoginRequest(string Email, string Password);
    public record GoogleLoginRequest(string IdToken);
    public record RefreshRequest(string RefreshToken);
}
