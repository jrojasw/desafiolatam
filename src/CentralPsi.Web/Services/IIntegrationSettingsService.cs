namespace CentralPsi.Web.Services;

/// <summary>Keys for values stored via IIntegrationSettingsService. Centralized here so the writer (an OAuth
/// callback, typically) and every reader agree on the exact same key.</summary>
public static class IntegrationSettingKeys
{
    public const string GoogleCalendarRefreshToken = "GoogleCalendar:RefreshToken";

    /// <summary>Timestamp (round-trip "O" format) of the last time CreateSessionEventAsync succeeded.</summary>
    public const string GoogleCalendarLastEventSuccessAtUtc = "GoogleCalendar:LastEventSuccessAtUtc";

    /// <summary>Timestamp of the last time CreateSessionEventAsync failed.</summary>
    public const string GoogleCalendarLastEventErrorAtUtc = "GoogleCalendar:LastEventErrorAtUtc";

    /// <summary>Type + message of that last failure, for display in the admin panel.</summary>
    public const string GoogleCalendarLastEventErrorMessage = "GoogleCalendar:LastEventErrorMessage";

    /// <summary>Timestamp of the last admin alert email sent for a Calendar failure - rate-limits that email
    /// independently of how often the underlying failure itself happens.</summary>
    public const string GoogleCalendarLastAdminAlertAtUtc = "GoogleCalendar:LastAdminAlertAtUtc";
}

public interface IIntegrationSettingsService
{
    /// <summary>Returns the decrypted value, or null if the key doesn't exist or fails to decrypt (e.g. the
    /// Data Protection key that encrypted it is gone) - callers should treat either case as "not set" and fall
    /// back accordingly, never throw.</summary>
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task<DateTime?> GetUpdatedAtUtcAsync(string key, CancellationToken ct = default);

    Task SetAsync(string key, string value, CancellationToken ct = default);
}
