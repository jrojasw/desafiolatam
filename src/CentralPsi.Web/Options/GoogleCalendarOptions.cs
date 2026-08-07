namespace CentralPsi.Web.Options;

public class GoogleCalendarOptions
{
    public const string SectionName = "GoogleCalendar";

    public bool Enabled { get; set; } = false;

    /// <summary>Path to the Google Cloud service account JSON key file.</summary>
    public string? ServiceAccountJsonPath { get; set; }

    /// <summary>
    /// Workspace user the service account impersonates via domain-wide delegation
    /// (required so created events carry a real organizer and Google Meet link, e.g. agenda@centralpsi.cl).
    /// </summary>
    public string? ImpersonateUser { get; set; }

    public string CalendarId { get; set; } = "primary";
}
