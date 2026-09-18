namespace CentralPsi.Web.Options;

/// <summary>Third-party analytics/ads tag IDs. Both are public client-side identifiers (visible in page
/// source), not secrets — but the tags themselves only load after the visitor accepts the cookie-consent
/// banner, per the commitment in Terms/Cookies.</summary>
public class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    /// <summary>GA4 measurement ID, format "G-XXXXXXXXXX". Empty disables Google Analytics entirely.</summary>
    public string GoogleAnalyticsId { get; set; } = "";

    /// <summary>Google Ads conversion tag ID, format "AW-XXXXXXXXX". Empty disables Google Ads conversion
    /// tracking entirely. Shares the same gtag.js loader as GoogleAnalyticsId when both are set.</summary>
    public string GoogleAdsId { get; set; } = "";

    /// <summary>Meta Pixel ID (numeric). Empty disables the Meta Pixel entirely.</summary>
    public string MetaPixelId { get; set; } = "";
}
