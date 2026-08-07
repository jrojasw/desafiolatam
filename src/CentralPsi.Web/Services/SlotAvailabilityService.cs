using CentralPsi.Web.Data;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Services;

public class SlotAvailabilityService : ISlotAvailabilityService
{
    private readonly ApplicationDbContext _db;
    private readonly ITimeZoneService _timeZoneService;
    private readonly AppOptions _appOptions;

    private static readonly TimeSpan MinimumLeadTime = TimeSpan.FromHours(2);

    public SlotAvailabilityService(ApplicationDbContext db, ITimeZoneService timeZoneService, IOptions<AppOptions> appOptions)
    {
        _db = db;
        _timeZoneService = timeZoneService;
        _appOptions = appOptions.Value;
    }

    public async Task<List<AvailableSlot>> GetAvailableSlotsAsync(Guid professionalId, int daysAhead = 21)
    {
        var availabilities = await _db.ProfessionalAvailabilities
            .Where(a => a.ProfessionalId == professionalId)
            .ToListAsync();

        if (availabilities.Count == 0)
        {
            return new List<AvailableSlot>();
        }

        var takenStarts = await _db.Appointments
            .Where(a => a.ProfessionalId == professionalId &&
                        (a.Status == AppointmentStatus.PendingPayment || a.Status == AppointmentStatus.Confirmed) &&
                        a.ScheduledStartUtc >= DateTime.UtcNow)
            .Select(a => a.ScheduledStartUtc)
            .ToListAsync();
        var takenSet = takenStarts.ToHashSet();

        var duration = TimeSpan.FromMinutes(_appOptions.SessionDurationMinutes);
        var nowLocal = _timeZoneService.ToLocal(DateTime.UtcNow);
        var earliestBookable = nowLocal.Add(MinimumLeadTime);

        var slots = new List<AvailableSlot>();
        for (var dayOffset = 0; dayOffset < daysAhead; dayOffset++)
        {
            var date = nowLocal.Date.AddDays(dayOffset);
            var windowsForDay = availabilities.Where(a => a.DayOfWeek == date.DayOfWeek);

            foreach (var window in windowsForDay)
            {
                var slotStartLocal = date.Add(window.StartTime);
                var windowEndLocal = date.Add(window.EndTime);

                while (slotStartLocal + duration <= windowEndLocal)
                {
                    if (slotStartLocal >= earliestBookable)
                    {
                        var startUtc = _timeZoneService.ToUtc(slotStartLocal);
                        if (!takenSet.Contains(startUtc))
                        {
                            slots.Add(new AvailableSlot(startUtc, startUtc.Add(duration)));
                        }
                    }

                    slotStartLocal = slotStartLocal.Add(duration);
                }
            }
        }

        return slots.OrderBy(s => s.StartUtc).ToList();
    }
}
