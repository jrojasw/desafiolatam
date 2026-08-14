namespace CentralPsi.Web.Services;

/// <summary>Periodically syncs pagos@centralpsi.cl into the admin panel. No-ops (with a one-time log) if
/// PaymentsInbox:Password isn't configured, so it's harmless to run everywhere including local dev.</summary>
public class PaymentInboxSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PaymentInboxSyncBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    public PaymentInboxSyncBackgroundService(IServiceProvider services, ILogger<PaymentInboxSyncBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IPaymentInboxSyncService>();
                if (sync.IsConfigured)
                {
                    var imported = await sync.SyncAsync(stoppingToken);
                    if (imported > 0)
                    {
                        _logger.LogInformation("Se importaron {Count} correos nuevos de pagos@centralpsi.cl", imported);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sincronizando la bandeja de pagos@centralpsi.cl");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
