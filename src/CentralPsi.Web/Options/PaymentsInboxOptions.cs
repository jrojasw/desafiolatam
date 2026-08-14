namespace CentralPsi.Web.Options;

/// <summary>
/// IMAP read-only access to the pagos@centralpsi.cl mailbox, so incoming boletas de honorarios show up in the
/// admin panel instead of requiring a login to cPanel's webmail. Read-only by design - messages are only ever
/// fetched and saved, never marked read/deleted/moved on the server, so nothing about the mailbox itself changes.
/// </summary>
public class PaymentsInboxOptions
{
    public const string SectionName = "PaymentsInbox";

    public string Host { get; set; } = "mail.centralpsi.cl";
    public int Port { get; set; } = 993;
    public string User { get; set; } = "pagos@centralpsi.cl";
    public string Password { get; set; } = string.Empty;
}
