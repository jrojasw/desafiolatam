namespace CentralPsi.Web.Models.Entities;

/// <summary>A copy of one email fetched (read-only) from pagos@centralpsi.cl via IMAP, so it shows up in the
/// admin panel instead of requiring a cPanel webmail login.</summary>
public class PaymentInboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>IMAP UID of the source message - used to avoid re-importing the same email on every sync.</summary>
    public string ImapUid { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyPreview { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;

    public bool Reviewed { get; set; }

    public List<PaymentInboxAttachment> Attachments { get; set; } = new();
}

public class PaymentInboxAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentInboxMessageId { get; set; }
    public PaymentInboxMessage? Message { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
}
