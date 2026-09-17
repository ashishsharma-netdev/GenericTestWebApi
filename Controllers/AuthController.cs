using System.Security.Claims;
using Google.Apis.Auth;
using GenericTestWebApi.Auth;
using GenericTestWebApi.Data;
using GenericTestWebApi.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(TestPrepDbContext db, JwtTokenService tokens, IConfiguration configuration) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email)) return Conflict(new { message="Email is already registered." });
        var user = new UserEntity { FullName=request.FullName.Trim(), Email=email };
        user.PasswordHash = new PasswordHasher<UserEntity>().HashPassword(user, request.Password);
        db.Users.Add(user); await db.SaveChangesAsync();
        var role = await db.Roles.SingleAsync(x=>x.Name=="FreeUser"); db.UserRoles.Add(new UserRoleEntity{UserId=user.Id,RoleId=role.Id}); await db.SaveChangesAsync();
        return Ok(await IssueTokens(user));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email=request.Email.Trim().ToLowerInvariant(); var user=await db.Users.Include(x=>x.UserRoles).ThenInclude(x=>x.Role).SingleOrDefaultAsync(x=>x.Email==email && x.IsActive);
        if(user is null || new PasswordHasher<UserEntity>().VerifyHashedPassword(user,user.PasswordHash,request.Password)!=PasswordVerificationResult.Success) return Unauthorized(new {message="Invalid email or password."});
        return Ok(await IssueTokens(user));
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(GoogleLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken)) return BadRequest(new { message = "Google ID token is required." });
        var clientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId)) return StatusCode(503, new { message = "Google Sign-In is not configured on the server." });

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            });
        }
        catch (InvalidJwtException)
        {
            return Unauthorized(new { message = "Google ID token is invalid or expired." });
        }

        if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
            return Unauthorized(new { message = "Google account email could not be verified." });

        var email = payload.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(x=>x.UserRoles).ThenInclude(x=>x.Role).SingleOrDefaultAsync(x=>x.GoogleSubject==payload.Subject);

        if (user is null)
        {
            user = await db.Users.Include(x=>x.UserRoles).ThenInclude(x=>x.Role).SingleOrDefaultAsync(x=>x.Email==email);
            if (user is not null)
            {
                if (!string.IsNullOrWhiteSpace(user.GoogleSubject) && user.GoogleSubject != payload.Subject)
                    return Conflict(new { message = "This email is already linked to another Google account." });
                user.GoogleSubject = payload.Subject;
                if (string.IsNullOrWhiteSpace(user.FullName)) user.FullName = payload.Name ?? email.Split('@')[0];
            }
            else
            {
                user = new UserEntity
                {
                    FullName = string.IsNullOrWhiteSpace(payload.Name) ? email.Split('@')[0] : payload.Name,
                    Email = email,
                    GoogleSubject = payload.Subject,
                    PasswordHash = new PasswordHasher<UserEntity>().HashPassword(null!, Guid.NewGuid().ToString("N"))
                };
                db.Users.Add(user);
                await db.SaveChangesAsync();
                var role = await db.Roles.SingleAsync(x=>x.Name=="FreeUser");
                db.UserRoles.Add(new UserRoleEntity { UserId=user.Id, RoleId=role.Id });
                await db.SaveChangesAsync();
                await db.Entry(user).Collection(x=>x.UserRoles).Query().Include(x=>x.Role).LoadAsync();
            }
            await db.SaveChangesAsync();
        }

        if (!user.IsActive) return Unauthorized(new { message = "This account is inactive." });
        return Ok(await IssueTokens(user));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var hash=tokens.HashRefreshToken(request.RefreshToken); var token=await db.RefreshTokens.Include(x=>x.User).ThenInclude(x=>x.UserRoles).ThenInclude(x=>x.Role).SingleOrDefaultAsync(x=>x.TokenHash==hash && x.RevokedAtUtc==null && x.ExpiresAtUtc>DateTime.UtcNow);
        if(token is null || !token.User.IsActive) return Unauthorized(new {message="Refresh token is invalid or expired."});
        token.RevokedAtUtc=DateTime.UtcNow; return Ok(await IssueTokens(token.User));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request){var hash=tokens.HashRefreshToken(request.RefreshToken);var token=await db.RefreshTokens.SingleOrDefaultAsync(x=>x.TokenHash==hash);if(token!=null){token.RevokedAtUtc=DateTime.UtcNow;await db.SaveChangesAsync();}return NoContent();}

    [HttpGet("me")]
    public async Task<IActionResult> Me(){if(!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id))return Unauthorized();var user=await db.Users.Include(x=>x.UserRoles).ThenInclude(x=>x.Role).AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id);if(user is null)return Unauthorized();return Ok(new{id=user.Id,fullName=user.FullName,email=user.Email,roles=user.UserRoles.Select(x=>x.Role.Name)});}

    private async Task<object> IssueTokens(UserEntity user)
    {
        var roles=user.UserRoles.Select(x=>x.Role.Name).ToList(); if(roles.Count==0) roles=await db.UserRoles.Where(x=>x.UserId==user.Id).Include(x=>x.Role).Select(x=>x.Role.Name).ToListAsync();
        var access=tokens.CreateAccessToken(user,roles);var refresh=tokens.CreateRefreshToken();db.RefreshTokens.Add(new RefreshTokenEntity{UserId=user.Id,TokenHash=refresh.Hash,ExpiresAtUtc=refresh.ExpiresAtUtc});await db.SaveChangesAsync();return new{accessToken=access,refreshToken=refresh.RawToken,expiresAtUtc=DateTime.UtcNow.AddMinutes(60),user=new{id=user.Id,fullName=user.FullName,email=user.Email,roles}};
    }
    public record RegisterRequest(string FullName,string Email,string Password);
    public record LoginRequest(string Email,string Password);
    public record GoogleLoginRequest(string IdToken);
    public record RefreshRequest(string RefreshToken);
}