using CentralPsi.Web.Data;
using CentralPsi.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Services;

/// <summary>
/// An appointment is created as PendingPayment the moment the patient submits the booking form, before Webpay
/// even loads - it's how the slot gets reserved during checkout. PaymentReturn frees it again on a declined or
/// cancelled payment, but if the patient just closes the tab and never comes back, no redirect ever fires to do
/// that. Without this cleanup, that slot would stay PendingPayment - and therefore permanently unbookable by
/// anyone, see SlotAvailabilityService - forever.
/// </summary>
public class StalePendingPaymentCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<StalePendingPaymentCleanupBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    public StalePendingPaymentCleanupBackgroundService(IServiceProvider services, ILogger<StalePendingPaymentCleanupBackgroundService> logger)
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
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error liberando horarios con pago pendiente abandonado");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoff = DateTime.UtcNow.Subtract(StaleAfter);
        var stale = await db.Appointments
            .Include(a => a.Payment)
            .Where(a => a.Status == AppointmentStatus.PendingPayment && a.CreatedAtUtc <= cutoff)
            .Take(100)
            .ToListAsync(ct);

        foreach (var appointment in stale)
        {
            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancelledAtUtc = DateTime.UtcNow;
            appointment.CancelledBy = "sistema (pago abandonado)";
            if (appointment.Payment is { Status: PaymentStatus.Initiated })
            {
                appointment.Payment.Status = PaymentStatus.Failed;
            }
        }

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Liberados {Count} horarios con pago abandonado", stale.Count);
        }
    }
}
