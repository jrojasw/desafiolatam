namespace CentralPsi.Web.Services;

public interface IWhatsAppNotificationService
{
    bool IsConfigured { get; }
    Task SendAsync(string message, CancellationToken ct = default);
}
