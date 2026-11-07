namespace CentralPsi.Web.Services;

/// <summary>Keys for values stored via IIntegrationSettingsService. Centralized here so the writer (an OAuth
/// callback, typically) and every reader agree on the exact same key.</summary>
public static class IntegrationSettingKeys
{
    public const string GoogleCalendarRefreshToken = "GoogleCalendar:RefreshToken";
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
