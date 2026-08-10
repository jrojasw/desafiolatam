namespace CentralPsi.Web.Options;

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "no-responder@centralpsi.cl";
    public string FromName { get; set; } = "CentralPsi";

    /// <summary>When true, emails are written to the log instead of sent (useful for local dev without SMTP creds).</summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Brevo transactional email API key. When set, emails are sent over HTTPS via Brevo's REST API instead
    /// of raw SMTP sockets - several PaaS hosts (Render included, based on repeated connection timeouts to
    /// two different providers) block outbound SMTP ports entirely, while HTTPS is essentially never blocked.
    /// </summary>
    public string? ApiKey { get; set; }
}
