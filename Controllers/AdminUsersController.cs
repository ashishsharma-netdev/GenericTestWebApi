using System.Security.Claims;
using GenericTestWebApi.Data;
using GenericTestWebApi.Entities;
using GenericTestWebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController(TestPrepDbContext db, AdminAuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Users([FromQuery] string? search, [FromQuery] string? status, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); query = query.Where(x => x.FullName.Contains(s) || x.Email.Contains(s)); }
        if (status == "Active") query = query.Where(x => x.IsActive);
        if (status == "Inactive") query = query.Where(x => !x.IsActive);
        var users = await query.OrderByDescending(x => x.CreatedAtUtc).Take(500).Select(x => new { x.Id, x.FullName, x.Email, x.IsActive, x.CreatedAtUtc, roles = x.UserRoles.Select(r => r.Role.Name).OrderBy(r => r).ToList() }).ToListAsync(ct);
        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> User(int id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return NotFound(new { message = "User not found." });
        var now = DateTime.UtcNow;
        var subscription = await db.Subscriptions.AsNoTracking().Include(x => x.Plan).Where(x => x.UserId == id).OrderByDescending(x => x.ExpiresAtUtc).FirstOrDefaultAsync(ct);
        var attempts = await db.TestAttempts.AsNoTracking().CountAsync(x => x.UserId == id, ct);
        return Ok(new { user.Id, user.FullName, user.Email, user.IsActive, user.CreatedAtUtc, roles = user.UserRoles.Select(x => x.Role.Name).OrderBy(x => x).ToList(), testAttempts = attempts, subscription = subscription is null ? null : new { subscription.Id, plan = subscription.Plan.Name, subscription.Status, subscription.StartedAtUtc, subscription.ExpiresAtUtc, isActive = subscription.Status == "Active" && subscription.ExpiresAtUtc > now } });
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, UserStatusRequest request, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return NotFound(new { message = "User not found." });
        if (!request.IsActive && await db.UserRoles.Include(x => x.Role).AnyAsync(x => x.UserId == id && x.Role.Name == "Admin", ct)) return BadRequest(new { message = "An administrator account cannot be deactivated from this screen." });
        user.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
            await audit.LogAsync(adminId, request.IsActive ? "USER_ACTIVATED" : "USER_DEACTIVATED", "User", id, $"User: {user.Email}", ct);
        return Ok(new { message = request.IsActive ? "User activated." : "User deactivated.", userId = id, isActive = user.IsActive });
    }

    public record UserStatusRequest(bool IsActive);
}
