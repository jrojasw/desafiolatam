namespace CentralPsi.Web.Services;

public interface IPaymentInboxSyncService
{
    bool IsConfigured { get; }

    /// <summary>Connects to pagos@centralpsi.cl over IMAP (read-only) and imports any message not already
    /// saved. Returns how many new messages were imported.</summary>
    Task<int> SyncAsync(CancellationToken ct = default);
}
