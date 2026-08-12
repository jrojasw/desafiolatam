namespace CentralPsi.Web.Options;

public class GoogleCalendarOptions
{
    public const string SectionName = "GoogleCalendar";

    public bool Enabled { get; set; } = false;

    /// <summary>
    /// OAuth path (preferred - works with any free Google account, no Workspace required). ClientId/ClientSecret
    /// come from a Google Cloud OAuth 2.0 Web application credential; RefreshToken is obtained once by an admin
    /// via /Admin/GoogleCalendar/Connect and then stored as an env var. Takes priority over the service-account
    /// fields below when all three are set.
    /// </summary>
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Service-account path (requires Google Workspace domain-wide delegation) - kept as an alternative for
    /// anyone who already has Workspace. The JSON key's raw content, meant to be set via an environment variable
    /// (GoogleCalendar__ServiceAccountJson) rather than committed to source control or written to a file on
    /// Render's ephemeral disk. Takes priority over ServiceAccountJsonPath when both are set.
    /// </summary>
    public string? ServiceAccountJson { get; set; }

    /// <summary>Path to the Google Cloud service account JSON key file - convenient for local development only.</summary>
    public string? ServiceAccountJsonPath { get; set; }

    /// <summary>
    /// Workspace user the service account impersonates via domain-wide delegation. Only relevant to the
    /// service-account path above.
    /// </summary>
    public string? ImpersonateUser { get; set; }

    public string CalendarId { get; set; } = "primary";
}
