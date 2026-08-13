namespace CentralPsi.Web.Options;

public class AppOptions
{
    public const string SectionName = "App";

    public decimal AppointmentPriceClp { get; set; } = 29750m;

    /// <summary>Flat amount CentralPsi pays the professional per session, out of AppointmentPriceClp.</summary>
    public decimal ProfessionalPayoutClp { get; set; } = 15000m;
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
}
