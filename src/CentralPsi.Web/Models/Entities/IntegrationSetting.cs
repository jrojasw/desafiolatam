namespace CentralPsi.Web.Models.Entities;

/// <summary>Small key/value store for runtime-set integration credentials (currently just the Google Calendar
/// OAuth refresh token) that need to survive redeploys without depending on a human correctly copy-pasting them
/// into Render's environment variables each time the connection is renewed. Value is encrypted at rest via
/// ASP.NET Core Data Protection (see IIntegrationSettingsService).</summary>
public class IntegrationSetting
{
    public string Key { get; set; } = string.Empty;
    public string EncryptedValue { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
