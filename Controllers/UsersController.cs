using System.Security.Claims;
using GenericTestWebApi.Data;
using GenericTestWebApi.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(TestPrepDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await GetCurrentUser();
        if (user is null) return Unauthorized();
        return Ok(ToDto(user));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest request)
    {
        var user = await GetCurrentUser();
        if (user is null) return Unauthorized();
        var fullName = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName)) return BadRequest(new { message = "Full name is required." });
        if (fullName.Length > 150) return BadRequest(new { message = "Full name cannot exceed 150 characters." });
        user.FullName = fullName;
        await db.SaveChangesAsync();
        return Ok(ToDto(user));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await GetCurrentUser();
        if (user is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { message = "Current password and new password are required." });
        if (request.NewPassword.Length < 6) return BadRequest(new { message = "New password must be at least 6 characters." });

        var hasher = new PasswordHasher<UserEntity>();
        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verification == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "Current password is incorrect." });

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync();
        return Ok(new { message = "Password changed successfully." });
    }

    private async Task<UserEntity?> GetCurrentUser()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
        return await db.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive);
    }

    private static object ToDto(UserEntity user) => new
    {
        id = user.Id,
        fullName = user.FullName,
        email = user.Email,
        createdAtUtc = user.CreatedAtUtc,
        roles = user.UserRoles.Select(x => x.Role.Name).OrderBy(x => x).ToList()
    };

    public record UpdateProfileRequest(string? FullName);
    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}
