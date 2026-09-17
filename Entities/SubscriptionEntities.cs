namespace GenericTestWebApi.Entities;

public class SubscriptionPlanEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "INR";
    public int DurationDays { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SubscriptionEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PlanId { get; set; }
    public string Provider { get; set; } = "Razorpay";
    public string? ProviderOrderId { get; set; }
    public string? ProviderPaymentId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public UserEntity User { get; set; } = null!;
    public SubscriptionPlanEntity Plan { get; set; } = null!;
    public ICollection<PaymentEntity> Payments { get; set; } = new List<PaymentEntity>();
}

public class PaymentEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? SubscriptionId { get; set; }
    public string Provider { get; set; } = "Razorpay";
    public string ProviderOrderId { get; set; } = "";
    public string? ProviderPaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Status { get; set; } = "Created";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAtUtc { get; set; }
    public UserEntity User { get; set; } = null!;
    public SubscriptionEntity? Subscription { get; set; }
}
