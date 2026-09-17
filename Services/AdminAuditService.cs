using GenericTestWebApi.Data;
using GenericTestWebApi.Entities;

namespace GenericTestWebApi.Services;

public class AdminAuditService(TestPrepDbContext db)
{
    public async Task LogAsync(int adminUserId, string action, string targetType, int? targetId, string? details, CancellationToken ct = default)
    {
        db.AdminAuditLogs.Add(new AdminAuditLogEntity
        {
            AdminUserId = adminUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Details = details,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}
