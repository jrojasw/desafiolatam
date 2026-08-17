namespace CentralPsi.Web.Options;

public class AppOptions
{
    public const string SectionName = "App";

    public decimal AppointmentPriceClp { get; set; } = 29750m;

    /// <summary>Flat amount CentralPsi pays the professional per session, out of AppointmentPriceClp.</summary>
    public decimal ProfessionalPayoutClp { get; set; } = 15000m;

    /// <summary>Business days CentralPsi commits to for paying the professional after a session, once the
    /// boleta arrives - drives the pending-payments "traffic light" in Admin/Pagos.</summary>
    public int ProfessionalPayoutBusinessDays { get; set; } = 3;
    public int SessionDurationMinutes { get; set; } = 50;
    public string TimeZoneId { get; set; } = "America/Santiago";
    public string AdminEmail { get; set; } = "admin@centralpsi.cl";
    public string RefundsEmail { get; set; } = "reembolsos@centralpsi.cl";

    /// <summary>Public base URL used to build absolute links in outgoing emails (e.g. https://centralpsi.cl).</summary>
    public string BaseUrl { get; set; } = "https://localhost:5001";

    /// <summary>Hours before the appointment above which a cancellation gets a 100% refund.</summary>
    public double FullRefundThresholdHours { get; set; } = 12;

    /// <summary>Maximum business days for the manual refund to be processed.</summary>
    public int RefundProcessingBusinessDays { get; set; } = 4;

    /// <summary>Global on/off switch for actually booking a session - turned off while showing the site to
    /// professionals before Transbank's production merchant account is connected, so nobody can pay for a
    /// session that can't yet be collected. Professionals and their profiles stay fully visible either way.</summary>
    public bool BookingEnabled { get; set; } = true;
}
