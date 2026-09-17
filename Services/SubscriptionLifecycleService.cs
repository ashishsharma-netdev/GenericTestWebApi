using GenericTestWebApi.Data;
using GenericTestWebApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Services;

public class SubscriptionLifecycleService(TestPrepDbContext db)
{
    public async Task<int> ExpireSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expired = await db.Subscriptions
            .Where(x => x.Status == "Active" && x.ExpiresAtUtc != null && x.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0) return 0;

        foreach (var subscription in expired)
            subscription.Status = "Expired";

        var userIds = expired.Select(x => x.UserId).Distinct().ToList();
        var premiumRole = await db.Roles.SingleOrDefaultAsync(x => x.Name == "PremiumUser", cancellationToken);
        var freeRole = await db.Roles.SingleOrDefaultAsync(x => x.Name == "FreeUser", cancellationToken);

        if (premiumRole is not null && freeRole is not null)
        {
            foreach (var userId in userIds)
            {
                var hasAnotherActive = await db.Subscriptions.AnyAsync(
                    x => x.UserId == userId && x.Status == "Active" && x.ExpiresAtUtc > now,
                    cancellationToken);

                if (hasAnotherActive) continue;

                var premium = await db.UserRoles.SingleOrDefaultAsync(
                    x => x.UserId == userId && x.RoleId == premiumRole.Id, cancellationToken);
                if (premium is not null) db.UserRoles.Remove(premium);

                var hasFree = await db.UserRoles.AnyAsync(
                    x => x.UserId == userId && x.RoleId == freeRole.Id, cancellationToken);
                if (!hasFree)
                    db.UserRoles.Add(new UserRoleEntity { UserId = userId, RoleId = freeRole.Id });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    public async Task ExpireUserSubscriptionsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expired = await db.Subscriptions
            .Where(x => x.UserId == userId && x.Status == "Active" && x.ExpiresAtUtc != null && x.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0) return;
        foreach (var subscription in expired) subscription.Status = "Expired";

        var hasAnotherActive = await db.Subscriptions.AnyAsync(
            x => x.UserId == userId && x.Status == "Active" && x.ExpiresAtUtc > now,
            cancellationToken);
        if (hasAnotherActive)
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var premiumRole = await db.Roles.SingleOrDefaultAsync(x => x.Name == "PremiumUser", cancellationToken);
        var freeRole = await db.Roles.SingleOrDefaultAsync(x => x.Name == "FreeUser", cancellationToken);
        if (premiumRole is null || freeRole is null)
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var premium = await db.UserRoles.SingleOrDefaultAsync(x => x.UserId == userId && x.RoleId == premiumRole.Id, cancellationToken);
        if (premium is not null) db.UserRoles.Remove(premium);
        if (!await db.UserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == freeRole.Id, cancellationToken))
            db.UserRoles.Add(new UserRoleEntity { UserId = userId, RoleId = freeRole.Id });

        await db.SaveChangesAsync(cancellationToken);
    }
}
