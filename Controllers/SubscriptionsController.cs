using System.Security.Claims;
using GenericTestWebApi.Data;
using GenericTestWebApi.Entities;
using GenericTestWebApi.Payments;
using GenericTestWebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionsController(TestPrepDbContext db, RazorpayService razorpay, SubscriptionLifecycleService lifecycle) : ControllerBase
{
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> Plans() => Ok(await db.SubscriptionPlans.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).Select(x => new { x.Id, x.Name, x.Code, x.Price, x.Currency, x.DurationDays }).ToListAsync());

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await lifecycle.ExpireUserSubscriptionsAsync(userId, cancellationToken);
        var subscription = await db.Subscriptions.AsNoTracking().Include(x => x.Plan).Where(x => x.UserId == userId && x.Status == "Active" && x.ExpiresAtUtc > DateTime.UtcNow).OrderByDescending(x => x.ExpiresAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (subscription is null) return Ok(new { isPremium = false, subscription = (object?)null });
        var daysRemaining = Math.Max(0, (int)Math.Ceiling((subscription.ExpiresAtUtc!.Value - DateTime.UtcNow).TotalDays));
        return Ok(new { isPremium = true, daysRemaining, isExpiringSoon = daysRemaining <= 7, subscription = new { subscription.Id, plan = subscription.Plan.Name, subscription.Plan.Code, subscription.Plan.Price, subscription.Plan.Currency, subscription.StartedAtUtc, subscription.ExpiresAtUtc, subscription.Status } });
    }

    [HttpGet("history")]
    [Authorize]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await lifecycle.ExpireUserSubscriptionsAsync(userId, cancellationToken);
        var items = await db.Subscriptions.AsNoTracking().Include(x => x.Plan).Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, plan = x.Plan.Name, code = x.Plan.Code, x.Status, x.StartedAtUtc, x.ExpiresAtUtc, x.CreatedAtUtc, providerOrderId = x.ProviderOrderId }).ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("payments")]
    [Authorize]
    public async Task<IActionResult> Payments(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var items = await db.Payments.AsNoTracking().Include(x => x.Subscription).Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.Amount, x.Currency, x.Status, x.Provider, x.ProviderOrderId, x.ProviderPaymentId, x.CreatedAtUtc, x.PaidAtUtc, subscriptionId = x.SubscriptionId }).ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost("create-order")]
    [Authorize]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await lifecycle.ExpireUserSubscriptionsAsync(userId, cancellationToken);
        var plan = await db.SubscriptionPlans.SingleOrDefaultAsync(x => x.Id == request.PlanId && x.IsActive, cancellationToken);
        if (plan is null) return NotFound(new { message = "Subscription plan not found." });
        if (!razorpay.IsConfigured) return StatusCode(503, new { message = "Razorpay is not configured on the server." });
        var receipt = $"tp_{userId}_{Guid.NewGuid():N}"[..30];
        var order = await razorpay.CreateOrderAsync(plan.Price, plan.Currency, receipt, cancellationToken);
        var subscription = new SubscriptionEntity { UserId = userId, PlanId = plan.Id, ProviderOrderId = order.Id, Status = "Pending" };
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);
        db.Payments.Add(new PaymentEntity { UserId = userId, SubscriptionId = subscription.Id, ProviderOrderId = order.Id, Amount = plan.Price, Currency = plan.Currency, Status = "Created" });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { orderId = order.Id, amount = order.Amount, currency = order.Currency, keyId = razorpay.PublicKeyId, plan = new { plan.Id, plan.Name, plan.Price, plan.Currency, plan.DurationDays } });
    }

    [HttpPost("verify")]
    [Authorize]
    public async Task<IActionResult> Verify(VerifyPaymentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var subscription = await db.Subscriptions.Include(x => x.Plan).SingleOrDefaultAsync(x => x.UserId == userId && x.ProviderOrderId == request.RazorpayOrderId, cancellationToken);
        if (subscription is null) return BadRequest(new { message = "Payment order was not created for this account." });
        if (!razorpay.VerifyPaymentSignature(request.RazorpayOrderId, request.RazorpayPaymentId, request.RazorpaySignature)) return BadRequest(new { message = "Payment signature verification failed." });
        if (await db.Payments.AnyAsync(x => x.ProviderPaymentId == request.RazorpayPaymentId, cancellationToken)) return Ok(new { message = "Payment was already processed.", isPremium = true });
        var payment = await db.Payments.SingleAsync(x => x.ProviderOrderId == request.RazorpayOrderId, cancellationToken);
        payment.ProviderPaymentId = request.RazorpayPaymentId; payment.Status = "Captured"; payment.PaidAtUtc = DateTime.UtcNow;
        await ActivateSubscriptionAsync(subscription, cancellationToken);
        return Ok(new { message = "Payment verified and Premium activated.", isPremium = true, expiresAtUtc = subscription.ExpiresAtUtc });
    }

    private async Task ActivateSubscriptionAsync(SubscriptionEntity subscription, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var baseDate = subscription.ExpiresAtUtc > now ? subscription.ExpiresAtUtc!.Value : now;
        subscription.StartedAtUtc ??= now; subscription.ExpiresAtUtc = baseDate.AddDays(subscription.Plan.DurationDays); subscription.Status = "Active";
        var premiumRole = await db.Roles.SingleAsync(x => x.Name == "PremiumUser", cancellationToken);
        var freeRole = await db.Roles.SingleAsync(x => x.Name == "FreeUser", cancellationToken);
        var roles = await db.UserRoles.Where(x => x.UserId == subscription.UserId).ToListAsync(cancellationToken);
        if (!roles.Any(x => x.RoleId == premiumRole.Id)) db.UserRoles.Add(new UserRoleEntity { UserId = subscription.UserId, RoleId = premiumRole.Id });
        foreach (var role in roles.Where(x => x.RoleId == freeRole.Id)) db.UserRoles.Remove(role);
        await db.SaveChangesAsync(cancellationToken);
    }

    private bool TryGetUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    public record CreateOrderRequest(int PlanId);
    public record VerifyPaymentRequest(string RazorpayOrderId, string RazorpayPaymentId, string RazorpaySignature);
}
