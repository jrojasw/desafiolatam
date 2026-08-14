using CentralPsi.Web.Data;
using CentralPsi.Web.Data.Seed;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Options;
using CentralPsi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Areas.Admin.Controllers;

/// <summary>
/// Manual payout tracking: professionals email their boleta de honorarios to pagos@centralpsi.cl after each
/// session, and an admin marks it paid here once the transfer to the professional's bank account (collected at
/// registration) is done. This is the admin-facing half of that flow - there is no automated email parsing yet.
/// </summary>
[Area("Admin")]
[Authorize(Roles = DataSeeder.AdminRole)]
[Route("Admin/Pagos")]
public class PaymentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _fileStorage;
    private readonly ITimeZoneService _timeZoneService;
    private readonly AppOptions _appOptions;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        ApplicationDbContext db,
        IFileStorageService fileStorage,
        ITimeZoneService timeZoneService,
        IOptions<AppOptions> appOptions,
        ILogger<PaymentsController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _timeZoneService = timeZoneService;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    /// <summary>Best-effort delete of a previously uploaded receipt when it's about to be replaced - not
    /// critical to the mark-paid flow, so a failure here is logged and swallowed rather than surfaced.</summary>
    private void TryDeleteOldReceipt(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        try
        {
            var physicalPath = _fileStorage.GetPrivatePhysicalPath(relativePath);
            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo borrar el comprobante anterior {Path}", relativePath);
        }
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string filter = "pendientes", string range = "todos")
    {
        var query = _db.Appointments
            .Include(a => a.Professional)
            .Where(a => a.Status == AppointmentStatus.Completed);

        query = filter switch
        {
            "pagados" => query.Where(a => a.ProfessionalPaidAtUtc != null),
            "todos" => query,
            _ => query.Where(a => a.ProfessionalPaidAtUtc == null)
        };

        var todayLocal = _timeZoneService.ToLocal(DateTime.UtcNow).Date;
        DateTime? fromLocal = range switch
        {
            "hoy" => todayLocal,
            "ayer" => todayLocal.AddDays(-1),
            "antesdeayer" => todayLocal.AddDays(-2),
            "semana" => todayLocal.AddDays(-((int)todayLocal.DayOfWeek == 0 ? 6 : (int)todayLocal.DayOfWeek - 1)),
            "mes" => new DateTime(todayLocal.Year, todayLocal.Month, 1),
            "anio" => new DateTime(todayLocal.Year, 1, 1),
            _ => null
        };
        DateTime? toLocalExclusive = range switch
        {
            "hoy" => todayLocal.AddDays(1),
            "ayer" => todayLocal,
            "antesdeayer" => todayLocal.AddDays(-1),
            "semana" => todayLocal.AddDays(1),
            "mes" => todayLocal.AddDays(1),
            "anio" => todayLocal.AddDays(1),
            _ => null
        };

        if (fromLocal.HasValue && toLocalExclusive.HasValue)
        {
            var fromUtc = _timeZoneService.ToUtc(fromLocal.Value);
            var toUtc = _timeZoneService.ToUtc(toLocalExclusive.Value);
            query = query.Where(a => a.ScheduledStartUtc >= fromUtc && a.ScheduledStartUtc < toUtc);
        }

        var appointments = await query
            .OrderByDescending(a => a.ScheduledStartUtc)
            .ToListAsync();

        ViewData["Filter"] = filter;
        ViewData["Range"] = range;
        ViewData["PayoutBusinessDays"] = _appOptions.ProfessionalPayoutBusinessDays;
        ViewData["TotalPendiente"] = await _db.Appointments
            .Where(a => a.Status == AppointmentStatus.Completed && a.ProfessionalPaidAtUtc == null)
            .SumAsync(a => a.ProfessionalPayoutAmount);

        return View(appointments);
    }

    [HttpPost("{id:guid}/MarcarPagado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(Guid id, string? note, IFormFile? receipt)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment is null) return NotFound();

        if (receipt is not { Length: > 0 })
        {
            TempData["ErrorMessage"] = "Debes adjuntar el comprobante de la transferencia para poder marcar la sesión como pagada.";
            return RedirectToAction(nameof(Index), new { filter = "pendientes" });
        }

        TryDeleteOldReceipt(appointment.ProfessionalPaymentReceiptPath);

        appointment.ProfessionalPaidAtUtc = DateTime.UtcNow;
        appointment.ProfessionalPaymentNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        appointment.ProfessionalPaymentReceiptPath = await _fileStorage.SavePrivateAsync(receipt, "comprobantes-pago");
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Pago marcado como realizado.";
        return RedirectToAction(nameof(Index), new { filter = "pendientes" });
    }

    [HttpPost("{id:guid}/MarcarPendiente")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPending(Guid id)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment is null) return NotFound();

        appointment.ProfessionalPaidAtUtc = null;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Pago vuelto a marcar como pendiente.";
        return RedirectToAction(nameof(Index), new { filter = "pagados" });
    }

    [HttpGet("{id:guid}/Comprobante")]
    public async Task<IActionResult> Receipt(Guid id)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment is null || string.IsNullOrEmpty(appointment.ProfessionalPaymentReceiptPath)) return NotFound();

        var physicalPath = _fileStorage.GetPrivatePhysicalPath(appointment.ProfessionalPaymentReceiptPath);
        if (!System.IO.File.Exists(physicalPath)) return NotFound();

        var contentType = Path.GetExtension(physicalPath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
        return File(bytes, contentType);
    }
}
