namespace GenericTestWebApi.Entities;

public class AdminAuditLogEntity
{
    public long Id { get; set; }
    public int AdminUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public int? TargetId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
