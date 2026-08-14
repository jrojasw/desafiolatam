using CentralPsi.Web.Data;
using CentralPsi.Web.Data.Seed;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Models.ViewModels;
using CentralPsi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Areas.Admin.Controllers;

/// <summary>
/// Financial overview: revenue (from authorized Transbank payments), professional payouts (from Admin/Pagos),
/// refunds (from cancellations) and an estimated tax/net-profit figure driven by an admin-editable tax rate -
/// CentralPsi's own tax structure (SII Inicio de Actividades) isn't finalized yet, so the tax figure is
/// explicitly an estimate the admin can tune, not a real tax calculation.
/// </summary>
[Area("Admin")]
[Authorize(Roles = DataSeeder.AdminRole)]
[Route("Admin/Finanzas")]
public class FinanceController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ITimeZoneService _timeZoneService;

    public FinanceController(ApplicationDbContext db, ITimeZoneService timeZoneService)
    {
        _db = db;
        _timeZoneService = timeZoneService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string range = "mes")
    {
        var settings = await _db.FinanceSettings.FindAsync(1) ?? new FinanceSettings();

        var todayLocal = _timeZoneService.ToLocal(DateTime.UtcNow).Date;
        var (fromLocal, toLocalExclusive) = ResolveRange(range, todayLocal);
        var fromUtc = fromLocal.HasValue ? _timeZoneService.ToUtc(fromLocal.Value) : (DateTime?)null;
        var toUtc = toLocalExclusive.HasValue ? _timeZoneService.ToUtc(toLocalExclusive.Value) : (DateTime?)null;

        var paymentsQuery = _db.Payments.Where(p => p.Status == PaymentStatus.Authorized && p.TransactionDateUtc != null);
        var payoutsQuery = _db.Appointments.Where(a => a.ProfessionalPaidAtUtc != null);
        var refundsQuery = _db.CancellationRequests.AsQueryable();

        if (fromUtc.HasValue && toUtc.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.TransactionDateUtc >= fromUtc && p.TransactionDateUtc < toUtc);
            payoutsQuery = payoutsQuery.Where(a => a.ProfessionalPaidAtUtc >= fromUtc && a.ProfessionalPaidAtUtc < toUtc);
            refundsQuery = refundsQuery.Where(c => c.RequestedAtUtc >= fromUtc && c.RequestedAtUtc < toUtc);
        }

        var payments = await paymentsQuery.ToListAsync();
        var payouts = await payoutsQuery.ToListAsync();
        var refunds = await refundsQuery.ToListAsync();

        var vm = new FinanceSummaryViewModel
        {
            Range = range,
            TaxRatePercent = settings.TaxRatePercent,
            SessionCount = payments.Count,
            TotalRevenue = payments.Sum(p => p.Amount),
            TotalProfessionalPayouts = payouts.Sum(a => a.ProfessionalPayoutAmount),
            TotalRefunds = refunds.Sum(c => c.RefundAmount)
        };

        // Daily chart: fixed rolling 30-day window regardless of the KPI range filter above.
        var dailyFromUtc = _timeZoneService.ToUtc(todayLocal.AddDays(-29));
        var allPaymentsForDaily = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Authorized && p.TransactionDateUtc != null && p.TransactionDateUtc >= dailyFromUtc)
            .ToListAsync();
        var allPayoutsForDaily = await _db.Appointments
            .Where(a => a.ProfessionalPaidAtUtc != null && a.ProfessionalPaidAtUtc >= dailyFromUtc)
            .ToListAsync();
        var allRefundsForDaily = await _db.CancellationRequests
            .Where(c => c.RequestedAtUtc >= dailyFromUtc)
            .ToListAsync();

        var revenueByDay = allPaymentsForDaily.GroupBy(p => _timeZoneService.ToLocal(p.TransactionDateUtc!.Value).Date).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        var payoutsByDay = allPayoutsForDaily.GroupBy(a => _timeZoneService.ToLocal(a.ProfessionalPaidAtUtc!.Value).Date).ToDictionary(g => g.Key, g => g.Sum(a => a.ProfessionalPayoutAmount));
        var refundsByDay = allRefundsForDaily.GroupBy(c => _timeZoneService.ToLocal(c.RequestedAtUtc).Date).ToDictionary(g => g.Key, g => g.Sum(c => c.RefundAmount));

        for (var d = todayLocal.AddDays(-29); d <= todayLocal; d = d.AddDays(1))
        {
            vm.DailySeries.Add(new FinanceDayPoint
            {
                DateLocal = d,
                Revenue = revenueByDay.GetValueOrDefault(d),
                Payouts = payoutsByDay.GetValueOrDefault(d),
                Refunds = refundsByDay.GetValueOrDefault(d)
            });
        }

        // Monthly chart/report: fixed rolling 12-month window.
        var monthlyFromLocal = new DateTime(todayLocal.Year, todayLocal.Month, 1).AddMonths(-11);
        var monthlyFromUtc = _timeZoneService.ToUtc(monthlyFromLocal);
        var allPaymentsForMonthly = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Authorized && p.TransactionDateUtc != null && p.TransactionDateUtc >= monthlyFromUtc)
            .ToListAsync();
        var allPayoutsForMonthly = await _db.Appointments
            .Where(a => a.ProfessionalPaidAtUtc != null && a.ProfessionalPaidAtUtc >= monthlyFromUtc)
            .ToListAsync();
        var allRefundsForMonthly = await _db.CancellationRequests
            .Where(c => c.RequestedAtUtc >= monthlyFromUtc)
            .ToListAsync();

        var revenueByMonth = allPaymentsForMonthly.GroupBy(p => new DateTime(_timeZoneService.ToLocal(p.TransactionDateUtc!.Value).Year, _timeZoneService.ToLocal(p.TransactionDateUtc!.Value).Month, 1))
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        var payoutsByMonth = allPayoutsForMonthly.GroupBy(a => new DateTime(_timeZoneService.ToLocal(a.ProfessionalPaidAtUtc!.Value).Year, _timeZoneService.ToLocal(a.ProfessionalPaidAtUtc!.Value).Month, 1))
            .ToDictionary(g => g.Key, g => g.Sum(a => a.ProfessionalPayoutAmount));
        var refundsByMonth = allRefundsForMonthly.GroupBy(c => new DateTime(_timeZoneService.ToLocal(c.RequestedAtUtc).Year, _timeZoneService.ToLocal(c.RequestedAtUtc).Month, 1))
            .ToDictionary(g => g.Key, g => g.Sum(c => c.RefundAmount));

        for (var m = monthlyFromLocal; m <= new DateTime(todayLocal.Year, todayLocal.Month, 1); m = m.AddMonths(1))
        {
            vm.MonthlySeries.Add(new FinanceMonthPoint
            {
                Year = m.Year,
                Month = m.Month,
                Revenue = revenueByMonth.GetValueOrDefault(m),
                Payouts = payoutsByMonth.GetValueOrDefault(m),
                Refunds = refundsByMonth.GetValueOrDefault(m),
                TaxRatePercent = settings.TaxRatePercent
            });
        }

        return View(vm);
    }

    [HttpPost("TasaImpuesto")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTaxRate(decimal taxRatePercent, string range = "mes")
    {
        if (taxRatePercent < 0) taxRatePercent = 0;
        if (taxRatePercent > 100) taxRatePercent = 100;

        var settings = await _db.FinanceSettings.FindAsync(1);
        if (settings is null)
        {
            settings = new FinanceSettings { Id = 1, TaxRatePercent = taxRatePercent };
            _db.FinanceSettings.Add(settings);
        }
        else
        {
            settings.TaxRatePercent = taxRatePercent;
        }
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Tasa de impuesto estimada actualizada.";
        return RedirectToAction(nameof(Index), new { range });
    }

    private static (DateTime? FromLocal, DateTime? ToLocalExclusive) ResolveRange(string range, DateTime todayLocal)
    {
        DateTime? fromLocal = range switch
        {
            "hoy" => todayLocal,
            "ayer" => todayLocal.AddDays(-1),
            "semana" => todayLocal.AddDays(-((int)todayLocal.DayOfWeek == 0 ? 6 : (int)todayLocal.DayOfWeek - 1)),
            "mes" => new DateTime(todayLocal.Year, todayLocal.Month, 1),
            "anio" => new DateTime(todayLocal.Year, 1, 1),
            _ => null
        };
        DateTime? toLocalExclusive = range switch
        {
            "hoy" => todayLocal.AddDays(1),
            "ayer" => todayLocal,
            "semana" => todayLocal.AddDays(1),
            "mes" => todayLocal.AddDays(1),
            "anio" => todayLocal.AddDays(1),
            _ => null
        };
        return (fromLocal, toLocalExclusive);
    }
}
