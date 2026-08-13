namespace CentralPsi.Web.Models.Entities;

public enum AppointmentStatus
{
    PendingPayment = 0,
    Confirmed = 1,
    Cancelled = 2,
    Completed = 3,
    Refunded = 4
}

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProfessionalId { get; set; }
    public Professional? Professional { get; set; }

    public string PatientFullName { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;

    /// <summary>Stored in UTC; convert with ITimeZoneService for display in America/Santiago.</summary>
    public DateTime ScheduledStartUtc { get; set; }
    public DateTime ScheduledEndUtc { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.PendingPayment;
    public decimal Amount { get; set; } = 29750m;

    public bool TermsAccepted { get; set; }
    public DateTime? TermsAcceptedAtUtc { get; set; }
    public string? TermsAcceptedIp { get; set; }

    // When the session is for a minor, PatientFullName/Email/Phone above stay the responsible adult's
    // (guardian) contact info - they're the one who books, pays, and receives all communications.
    public bool IsForMinor { get; set; }
    public string? MinorFullName { get; set; }
    public int? MinorAge { get; set; }
    public string? GuardianRelationship { get; set; }
    public DateTime? GuardianConsentAcceptedAtUtc { get; set; }

    public string? GoogleEventId { get; set; }
    public string? GoogleMeetLink { get; set; }

    // Session-happened confirmation, kept as an audit trail backing each payment.
    public string PatientAttendanceToken { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime? PatientAttendanceConfirmedAtUtc { get; set; }
    public bool? PatientConfirmsSessionHappened { get; set; }

    public string ProfessionalAttendanceToken { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime? ProfessionalAttendanceConfirmedAtUtc { get; set; }
    public bool? ProfessionalConfirmsSessionHappened { get; set; }

    public DateTime? AttendanceRequestSentAtUtc { get; set; }

    public string CancellationToken { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelledBy { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Manual payout tracking: the professional emails their boleta to pagos@centralpsi.cl after each session,
    // and an admin marks it paid here once the transfer to the professional's bank account is done.
    public decimal ProfessionalPayoutAmount { get; set; }
    public DateTime? ProfessionalPaidAtUtc { get; set; }
    public string? ProfessionalPaymentNote { get; set; }

    public Payment? Payment { get; set; }
    public CancellationRequest? CancellationRequest { get; set; }
}
