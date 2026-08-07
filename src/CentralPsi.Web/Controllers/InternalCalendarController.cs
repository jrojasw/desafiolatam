using CentralPsi.Web.Data;
using CentralPsi.Web.Data.Seed;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Models.ViewModels;
using CentralPsi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Controllers;

/// <summary>
/// Standalone operations calendar: intentionally not linked from any public page or from the admin
/// dashboard's navigation (per the internal requirement that it stay a separate, unlinked page). It still
/// requires the same admin login as the dashboard - "not linked" means undiscoverable via navigation, not
/// unauthenticated.
/// </summary>
[Authorize(Roles = DataSeeder.AdminRole)]
[Route("panel-interno/calendario")]
public class InternalCalendarController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ITimeZoneService _timeZoneService;

    public InternalCalendarController(ApplicationDbContext db, ITimeZoneService timeZoneService)
    {
        _db = db;
        _timeZoneService = timeZoneService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var fromLocal = (from ?? _timeZoneService.ToLocal(DateTime.UtcNow).Date.AddDays(-7)).Date;
        var toLocal = (to ?? fromLocal.AddDays(37)).Date;
        var fromUtc = _timeZoneService.ToUtc(fromLocal);
        var toUtc = _timeZoneService.ToUtc(toLocal);

        var appointments = await _db.Appointments
            .Include(a => a.Professional)
            .Include(a => a.Payment)
            .Where(a => a.ScheduledStartUtc >= fromUtc && a.ScheduledStartUtc < toUtc)
            .OrderBy(a => a.ScheduledStartUtc)
            .ToListAsync();

        var vm = new InternalCalendarViewModel
        {
            FromLocal = fromLocal,
            ToLocal = toLocal,
            Rows = appointments.Where(a => a.Professional is not null).Select(a => new InternalCalendarRow
            {
                Appointment = a,
                Professional = a.Professional!,
                StartLocal = _timeZoneService.ToLocal(a.ScheduledStartUtc),
                IsPaid = a.Payment?.Status == PaymentStatus.Authorized
            }).ToList()
        };

        return View(vm);
    }
}
