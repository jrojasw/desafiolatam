namespace CentralPsi.Web.Models.Entities;

public enum PaymentStatus
{
    Initiated = 0,
    Authorized = 1,
    Failed = 2,
    Reversed = 3
}

/// <summary>Transbank Webpay Plus transaction record, kept as the backup/audit trail for a payment.</summary>
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public string BuyOrder { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string? Token { get; set; }

    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Initiated;

    public string? AuthorizationCode { get; set; }
    public int? ResponseCode { get; set; }
    public DateTime? TransactionDateUtc { get; set; }
    public string? RawResponseJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
