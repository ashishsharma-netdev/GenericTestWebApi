using GenericTestWebApi.Data;
using GenericTestWebApi.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/admin/subscriptions")]
[Authorize(Roles = "Admin")]
public class AdminSubscriptionsController(TestPrepDbContext db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var active = await db.Subscriptions.CountAsync(x => x.Status == "Active" && x.ExpiresAtUtc > now, ct);
        var expired = await db.Subscriptions.CountAsync(x => x.Status == "Expired" || (x.Status == "Active" && x.ExpiresAtUtc <= now), ct);
        var successful = await db.Payments.CountAsync(x => x.Status == "Captured", ct);
        var revenue = await db.Payments.Where(x => x.Status == "Captured").SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        return Ok(new { totalUsers = await db.Users.CountAsync(ct), activePremiumUsers = active, activeSubscriptions = active, expiredSubscriptions = expired, successfulPayments = successful, revenue });
    }

    [HttpGet]
    public async Task<IActionResult> Subscriptions([FromQuery] string? search, [FromQuery] string? status, CancellationToken ct)
    {
        var query = db.Subscriptions.AsNoTracking().Include(x => x.User).Include(x => x.Plan).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x => x.User.FullName.Contains(s) || x.User.Email.Contains(s) || (x.ProviderOrderId != null && x.ProviderOrderId.Contains(s)) || (x.ProviderPaymentId != null && x.ProviderPaymentId.Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(status) && status != "All") query = query.Where(x => x.Status == status);
        return Ok(await query.OrderByDescending(x => x.CreatedAtUtc).Take(500).Select(x => new { x.Id, userId = x.UserId, user = x.User.FullName, email = x.User.Email, plan = x.Plan.Name, planId = x.PlanId, x.Provider, x.ProviderOrderId, x.ProviderPaymentId, x.Status, x.StartedAtUtc, x.ExpiresAtUtc, x.CreatedAtUtc }).ToListAsync(ct));
    }

    [HttpGet("payments")]
    public async Task<IActionResult> Payments([FromQuery] string? search, [FromQuery] string? status, CancellationToken ct)
    {
        var query = db.Payments.AsNoTracking().Include(x => x.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x => x.User.FullName.Contains(s) || x.User.Email.Contains(s) || x.ProviderOrderId.Contains(s) || (x.ProviderPaymentId != null && x.ProviderPaymentId.Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(status) && status != "All") query = query.Where(x => x.Status == status);
        return Ok(await query.OrderByDescending(x => x.CreatedAtUtc).Take(500).Select(x => new { x.Id, userId = x.UserId, user = x.User.FullName, email = x.User.Email, x.SubscriptionId, x.Provider, x.ProviderOrderId, x.ProviderPaymentId, x.Amount, x.Currency, x.Status, x.CreatedAtUtc, x.PaidAtUtc }).ToListAsync(ct));
    }

    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken ct) => Ok(await db.SubscriptionPlans.AsNoTracking().OrderBy(x => x.DisplayOrder).ToListAsync(ct));

    [HttpPut("plans/{id:int}")]
    public async Task<IActionResult> UpdatePlan(int id, PlanRequest request, CancellationToken ct)
    {
        var plan = await db.SubscriptionPlans.FindAsync([id], ct);
        if (plan is null) return NotFound(new { message = "Plan not found." });
        if (request.Price < 0 || request.DurationDays <= 0) return BadRequest(new { message = "Price and duration must be valid." });
        plan.Name = request.Name.Trim(); plan.Price = request.Price; plan.Currency = request.Currency.Trim().ToUpperInvariant(); plan.DurationDays = request.DurationDays; plan.DisplayOrder = request.DisplayOrder; plan.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Ok(plan);
    }

    [HttpPost("manual-activate")]
    public async Task<IActionResult> ManualActivate(ManualActivateRequest request, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([request.UserId], ct);
        var plan = await db.SubscriptionPlans.FindAsync([request.PlanId], ct);
        if (user is null) return NotFound(new { message = "User not found." });
        if (plan is null || !plan.IsActive) return BadRequest(new { message = "Active subscription plan not found." });
        var now = DateTime.UtcNow;
        var existing = await db.Subscriptions.Where(x => x.UserId == user.Id && x.Status == "Active").OrderByDescending(x => x.ExpiresAtUtc).FirstOrDefaultAsync(ct);
        if (existing is not null && existing.ExpiresAtUtc > now)
        {
            existing.ExpiresAtUtc = existing.ExpiresAtUtc.Value.AddDays(request.DurationDaysOverride ?? plan.DurationDays);
            existing.PlanId = plan.Id;
        }
        else
        {
            existing = new SubscriptionEntity { UserId = user.Id, PlanId = plan.Id, Provider = "Admin", Status = "Active", StartedAtUtc = now, ExpiresAtUtc = now.AddDays(request.DurationDaysOverride ?? plan.DurationDays) };
            db.Subscriptions.Add(existing);
        }
        await SetPremiumRoleAsync(user.Id, ct);
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Premium activated manually.", subscriptionId = existing.Id, expiresAtUtc = existing.ExpiresAtUtc });
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var subscription = await db.Subscriptions.FindAsync([id], ct);
        if (subscription is null) return NotFound(new { message = "Subscription not found." });
        subscription.Status = "Cancelled";
        subscription.ExpiresAtUtc = DateTime.UtcNow;
        await RemovePremiumRoleIfNoActiveSubscriptionAsync(subscription.UserId, ct);
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Subscription cancelled." });
    }

    private async Task SetPremiumRoleAsync(int userId, CancellationToken ct)
    {
        var premium = await db.Roles.SingleAsync(x => x.Name == "PremiumUser", ct);
        var free = await db.Roles.SingleAsync(x => x.Name == "FreeUser", ct);
        if (!await db.UserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == premium.Id, ct)) db.UserRoles.Add(new UserRoleEntity { UserId = userId, RoleId = premium.Id });
        var freeRoles = await db.UserRoles.Where(x => x.UserId == userId && x.RoleId == free.Id).ToListAsync(ct);
        db.UserRoles.RemoveRange(freeRoles);
    }

    private async Task RemovePremiumRoleIfNoActiveSubscriptionAsync(int userId, CancellationToken ct)
    {
        var hasActive = await db.Subscriptions.AnyAsync(x => x.UserId == userId && x.Status == "Active" && x.ExpiresAtUtc > DateTime.UtcNow, ct);
        if (hasActive) return;
        var premium = await db.Roles.SingleAsync(x => x.Name == "PremiumUser", ct);
        var free = await db.Roles.SingleAsync(x => x.Name == "FreeUser", ct);
        var role = await db.UserRoles.SingleOrDefaultAsync(x => x.UserId == userId && x.RoleId == premium.Id, ct);
        if (role is not null) db.UserRoles.Remove(role);
        if (!await db.UserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == free.Id, ct)) db.UserRoles.Add(new UserRoleEntity { UserId = userId, RoleId = free.Id });
    }

    public record PlanRequest(string Name, decimal Price, string Currency, int DurationDays, int DisplayOrder, bool IsActive);
    public record ManualActivateRequest(int UserId, int PlanId, int? DurationDaysOverride);
}
