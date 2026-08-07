using CentralPsi.Web.Areas.Admin.Models;
using CentralPsi.Web.Data;
using CentralPsi.Web.Data.Seed;
using CentralPsi.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = DataSeeder.AdminRole)]
[Route("Admin")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    [HttpGet("Dashboard")]
    public async Task<IActionResult> Index()
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var vm = new DashboardViewModel
        {
            PendingProfessionals = await _db.Professionals.CountAsync(p => p.Status == ProfessionalStatus.PendingVerification),
            VerifiedProfessionals = await _db.Professionals.CountAsync(p => p.Status == ProfessionalStatus.Verified),
            UpcomingAppointments = await _db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Confirmed && a.ScheduledStartUtc >= DateTime.UtcNow),
            PendingRefunds = await _db.CancellationRequests.CountAsync(c => c.Status == RefundStatus.PendingManualProcessing),
            RevenueThisMonth = await _db.Payments
                .Where(p => p.Status == PaymentStatus.Authorized && p.TransactionDateUtc >= monthStart)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m
        };

        return View(vm);
    }
}
