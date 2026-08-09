using CentralPsi.Web.Data;
using CentralPsi.Web.Data.Seed;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = DataSeeder.AdminRole)]
[Route("Admin/Professionals")]
public class ProfessionalsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _fileStorage;
    private readonly INotificationService _notifications;
    private readonly ICertificateValidationService _certificateValidation;

    public ProfessionalsController(
        ApplicationDbContext db,
        IFileStorageService fileStorage,
        INotificationService notifications,
        ICertificateValidationService certificateValidation)
    {
        _db = db;
        _fileStorage = fileStorage;
        _notifications = notifications;
        _certificateValidation = certificateValidation;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(ProfessionalStatus? status)
    {
        var query = _db.Professionals.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }
        ViewData["StatusFilter"] = status;
        var professionals = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return View(professionals);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var professional = await _db.Professionals
            .Include(p => p.Availabilities)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (professional is null) return NotFound();
        return View(professional);
    }

    [HttpGet("{id:guid}/Documento/{type}")]
    public async Task<IActionResult> Document(Guid id, string type)
    {
        var professional = await _db.Professionals.FindAsync(id);
        if (professional is null) return NotFound();

        var relativePath = type switch
        {
            "cedula-frente" => professional.CedulaFrontPath,
            "cedula-reverso" => professional.CedulaBackPath,
            "certificado" => professional.CertificateFilePath,
            _ => null
        };
        if (relativePath is null) return NotFound();

        var physicalPath = _fileStorage.GetPrivatePhysicalPath(relativePath);
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

    [HttpPost("{id:guid}/ReintentarValidacion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryValidation(Guid id)
    {
        var professional = await _db.Professionals.FindAsync(id);
        if (professional is null) return NotFound();

        var physicalPath = _fileStorage.GetPrivatePhysicalPath(professional.CertificateFilePath);
        var result = await _certificateValidation.ValidateAsync(professional.CertificateValidationCode, physicalPath);

        professional.CertificateQrRawData = result.QrRawData;
        professional.CertificateVerificationNotes = result.Notes;
        professional.CertificateVerifiedAt = DateTime.UtcNow;

        if (result.IsValid && !result.Inconclusive && professional.Status == ProfessionalStatus.PendingVerification)
        {
            professional.Status = ProfessionalStatus.Verified;
            await _db.SaveChangesAsync();
            await _notifications.SendProfessionalVerifiedAsync(professional);
            TempData["SuccessMessage"] = $"¡Validación automática exitosa! {professional.FullName} quedó publicado.";
        }
        else
        {
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Se volvió a intentar la validación automática - revisa la nota actualizada abajo.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/Aprobar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id)
    {
        var professional = await _db.Professionals.FindAsync(id);
        if (professional is null) return NotFound();

        professional.Status = ProfessionalStatus.Verified;
        professional.CertificateVerifiedAt = DateTime.UtcNow;
        professional.CertificateVerificationNotes = (professional.CertificateVerificationNotes ?? "") + " [Aprobado manualmente por el administrador]";
        await _db.SaveChangesAsync();
        await _notifications.SendProfessionalVerifiedAsync(professional);

        TempData["SuccessMessage"] = $"{professional.FullName} fue publicado en el listado de profesionales.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/Rechazar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, string? reason)
    {
        var professional = await _db.Professionals.FindAsync(id);
        if (professional is null) return NotFound();

        professional.Status = ProfessionalStatus.Rejected;
        await _db.SaveChangesAsync();
        await _notifications.SendProfessionalRejectedAsync(professional, reason ?? "No cumple los requisitos de validación.");

        TempData["SuccessMessage"] = $"{professional.FullName} fue rechazado.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/Desactivar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var professional = await _db.Professionals.FindAsync(id);
        if (professional is null) return NotFound();

        professional.Status = ProfessionalStatus.Inactive;
        professional.DeactivatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"{professional.FullName} fue retirado del listado público.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/Reactivar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        var professional = await _db.Professionals.FindAsync(id);
        if (professional is null) return NotFound();

        professional.Status = ProfessionalStatus.Verified;
        professional.DeactivatedAt = null;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"{professional.FullName} vuelve a estar publicado.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/Eliminar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var professional = await _db.Professionals.FindAsync(id);
        if (professional is null) return NotFound();

        var hasAppointments = await _db.Appointments.AnyAsync(a => a.ProfessionalId == id);
        if (hasAppointments)
        {
            TempData["ErrorMessage"] = "No se puede eliminar: este profesional tiene citas asociadas. Puedes desactivarlo en su lugar.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Professionals.Remove(professional);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Profesional eliminado.";
        return RedirectToAction(nameof(Index));
    }
}
