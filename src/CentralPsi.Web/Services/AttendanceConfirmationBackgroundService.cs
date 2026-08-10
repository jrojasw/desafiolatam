using CentralPsi.Web.Data;
using CentralPsi.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Services;

/// <summary>
/// Once a confirmed appointment's scheduled end time has passed, marks it Completed and emails both parties a
/// one-click link asking them to confirm the session actually happened - this double confirmation is the audit
/// trail CentralPsi keeps to back up each payment, since there is no reliable API signal for Meet attendance
/// without full Google Workspace admin access.
/// </summary>
public class AttendanceConfirmationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AttendanceConfirmationBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    public AttendanceConfirmationBackgroundService(IServiceProvider services, ILogger<AttendanceConfirmationBackgroundService> logger)
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
                _logger.LogError(ex, "Error procesando confirmaciones de asistencia pendientes");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var due = await db.Appointments
            .Include(a => a.Professional)
            .Where(a => a.Status == AppointmentStatus.Confirmed
                        && a.ScheduledEndUtc <= DateTime.UtcNow
                        && a.AttendanceRequestSentAtUtc == null)
            .OrderBy(a => a.ScheduledEndUtc)
            .Take(50)
            .ToListAsync(ct);

        foreach (var appointment in due)
        {
            if (appointment.Professional is null) continue;

            appointment.Status = AppointmentStatus.Completed;
            appointment.AttendanceRequestSentAtUtc = DateTime.UtcNow;
            await notifications.SendAttendanceConfirmationRequestAsync(appointment, appointment.Professional);
        }

        if (due.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
