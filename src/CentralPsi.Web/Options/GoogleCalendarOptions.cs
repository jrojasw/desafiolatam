namespace CentralPsi.Web.Options;

public class GoogleCalendarOptions
{
    public const string SectionName = "GoogleCalendar";

    public bool Enabled { get; set; } = false;

    /// <summary>
    /// The service account JSON key's raw content, meant to be set via an environment variable
    /// (GoogleCalendar__ServiceAccountJson) rather than committed to source control or written to a file on
    /// Render's ephemeral disk. Takes priority over ServiceAccountJsonPath when both are set.
    /// </summary>
    public string? ServiceAccountJson { get; set; }

    /// <summary>Path to the Google Cloud service account JSON key file - convenient for local development only.</summary>
    public string? ServiceAccountJsonPath { get; set; }

    /// <summary>
    /// Workspace user the service account impersonates via domain-wide delegation
    /// (required so created events carry a real organizer and Google Meet link, e.g. agenda@centralpsi.cl).
    /// </summary>
    public string? ImpersonateUser { get; set; }

    public string CalendarId { get; set; } = "primary";
}
