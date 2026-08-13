using CentralPsi.Web.Data;
using CentralPsi.Web.Data.Seed;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    public PaymentsController(ApplicationDbContext db, IFileStorageService fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string filter = "pendientes")
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

        var appointments = await query
            .OrderByDescending(a => a.ScheduledStartUtc)
            .ToListAsync();

        ViewData["Filter"] = filter;
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

        appointment.ProfessionalPaidAtUtc = DateTime.UtcNow;
        appointment.ProfessionalPaymentNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (receipt is { Length: > 0 })
        {
            appointment.ProfessionalPaymentReceiptPath = await _fileStorage.SavePrivateAsync(receipt, "comprobantes-pago");
        }
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

    [HttpPost("{id:guid}/SubirComprobante")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadReceipt(Guid id, IFormFile receipt)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment is null) return NotFound();

        if (receipt is not { Length: > 0 })
        {
            TempData["ErrorMessage"] = "Selecciona un archivo (PDF o imagen) antes de subir.";
            return RedirectToAction(nameof(Index), new { filter = "pagados" });
        }

        appointment.ProfessionalPaymentReceiptPath = await _fileStorage.SavePrivateAsync(receipt, "comprobantes-pago");
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Comprobante de pago subido.";
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
