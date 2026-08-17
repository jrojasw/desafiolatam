namespace CentralPsi.Web.Options;

/// <summary>
/// WhatsApp notifications via CallMeBot (api.callmebot.com) - a free, unofficial "send yourself a WhatsApp
/// message via HTTP GET" service, set up by messaging their bot number once to get an API key. Not an
/// enterprise-grade integration (no SLA, could stop working if WhatsApp changes policy), but the simplest way
/// to get a WhatsApp ping for something like "a new professional just registered".
/// </summary>
public class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>Destination phone number in international format without "+" (e.g. 56912345678).</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>API key CallMeBot's bot replies with after you message it "I allow callmebot to send me messages".</summary>
    public string ApiKey { get; set; } = string.Empty;
}
