namespace CentralPsi.Web.Models.Entities;

public enum RefundTier
{
    /// <summary>Cancelled 12+ hours before the appointment: 100% refund.</summary>
    Full100 = 0,
    /// <summary>Cancelled between 12 and 0 hours before the appointment: 50% refund.</summary>
    Partial50 = 1,
    /// <summary>Cancelled at/after the appointment start: not eligible, subject to manual review.</summary>
    NotEligible = 2
}

public enum RefundStatus
{
    PendingManualProcessing = 0,
    Processed = 1
}

/// <summary>
/// Refunds are always processed manually (bank transfer) - this record captures the calculated
/// tier and is what gets emailed to reembolsos@centralpsi.cl for the finance team to action.
/// </summary>
public class CancellationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public double HoursBeforeAppointment { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string? Reason { get; set; }

    public RefundTier RefundTier { get; set; }
    public decimal RefundAmount { get; set; }
    public RefundStatus Status { get; set; } = RefundStatus.PendingManualProcessing;

    public string? PatientBankDetails { get; set; }
}
