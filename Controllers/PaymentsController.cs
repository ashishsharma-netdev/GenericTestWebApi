using System.Text.Json;
using GenericTestWebApi.Data;
using GenericTestWebApi.Entities;
using GenericTestWebApi.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(TestPrepDbContext db, RazorpayService razorpay, IConfiguration configuration) : ControllerBase
{
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        var secret = configuration["Razorpay:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret)) return StatusCode(503);
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        if (!razorpay.VerifyWebhookSignature(rawBody, Request.Headers["X-Razorpay-Signature"].ToString(), secret)) return Unauthorized();
        using var json = JsonDocument.Parse(rawBody);
        if (!json.RootElement.TryGetProperty("event", out var eventElement) || eventElement.GetString() != "payment.captured") return Ok(new { received = true });
        var entity = json.RootElement.GetProperty("payload").GetProperty("payment").GetProperty("entity");
        var orderId = entity.GetProperty("order_id").GetString(); var paymentId = entity.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(paymentId) || await db.Payments.AnyAsync(x => x.ProviderPaymentId == paymentId, cancellationToken)) return Ok(new { received = true });
        var payment = await db.Payments.Include(x => x.Subscription).ThenInclude(x => x!.Plan).SingleOrDefaultAsync(x => x.ProviderOrderId == orderId, cancellationToken);
        if (payment?.Subscription is null) return Ok(new { received = true });
        payment.ProviderPaymentId = paymentId; payment.Status = "Captured"; payment.PaidAtUtc = DateTime.UtcNow;
        var subscription = payment.Subscription; var now = DateTime.UtcNow; var baseDate = subscription.ExpiresAtUtc > now ? subscription.ExpiresAtUtc!.Value : now;
        subscription.StartedAtUtc ??= now; subscription.ExpiresAtUtc = baseDate.AddDays(subscription.Plan.DurationDays); subscription.Status = "Active";
        var premiumRole = await db.Roles.SingleAsync(x => x.Name == "PremiumUser", cancellationToken); var freeRole = await db.Roles.SingleAsync(x => x.Name == "FreeUser", cancellationToken);
        var roles = await db.UserRoles.Where(x => x.UserId == subscription.UserId).ToListAsync(cancellationToken);
        if (!roles.Any(x => x.RoleId == premiumRole.Id)) db.UserRoles.Add(new UserRoleEntity { UserId = subscription.UserId, RoleId = premiumRole.Id });
        foreach (var role in roles.Where(x => x.RoleId == freeRole.Id)) db.UserRoles.Remove(role);
        await db.SaveChangesAsync(cancellationToken); return Ok(new { received = true });
    }
}
