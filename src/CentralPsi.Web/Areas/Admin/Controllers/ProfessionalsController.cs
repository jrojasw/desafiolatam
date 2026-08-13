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
    private readonly IGoogleCalendarService _googleCalendar;
    private readonly ILogger<ProfessionalsController> _logger;

    public ProfessionalsController(
        ApplicationDbContext db,
        IFileStorageService fileStorage,
        INotificationService notifications,
        ICertificateValidationService certificateValidation,
        IGoogleCalendarService googleCalendar,
        ILogger<ProfessionalsController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _notifications = notifications;
        _certificateValidation = certificateValidation;
        _googleCalendar = googleCalendar;
        _logger = logger;
    }

    /// <summary>Notification failures (bad SMTP config, provider outage) shouldn't turn an otherwise-successful
    /// admin action (approve/reject/etc., already saved to the DB) into a 500 error page.</summary>
    private async Task TrySendNotificationAsync(Func<Task> send, Guid professionalId)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando notificación por correo para el profesional {ProfessionalId}", professionalId);
        }
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

        ViewBag.Appointments = await _db.Appointments
            .Where(a => a.ProfessionalId == id)
            .OrderByDescending(a => a.ScheduledStartUtc)
            .ToListAsync();

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
            await TrySendNotificationAsync(() => _notifications.SendProfessionalVerifiedAsync(professional), professional.Id);
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
        await TrySendNotificationAsync(() => _notifications.SendProfessionalVerifiedAsync(professional), professional.Id);

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
        await TrySendNotificationAsync(
            () => _notifications.SendProfessionalRejectedAsync(professional, reason ?? "No cumple los requisitos de validación."),
            professional.Id);

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

    /// <summary>
    /// Admin-initiated cancellation for when a professional has to be removed while they still have a
    /// future appointment booked - unlike the patient's own cancellation link, this is never the patient's
    /// fault, so it always grants a full refund regardless of how much notice there was.
    /// </summary>
    [HttpPost("{id:guid}/Citas/{appointmentId:guid}/Cancelar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAppointment(Guid id, Guid appointmentId)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Professional)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.ProfessionalId == id);
        if (appointment?.Professional is null) return NotFound();

        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Completed or AppointmentStatus.Refunded)
        {
            TempData["ErrorMessage"] = "Esta cita ya no se puede cancelar (su estado actual no lo permite).";
            return RedirectToAction(nameof(Details), new { id });
        }

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAtUtc = DateTime.UtcNow;
        appointment.CancelledBy = "admin";

        var cancellationRequest = new CancellationRequest
        {
            AppointmentId = appointment.Id,
            HoursBeforeAppointment = (appointment.ScheduledStartUtc - DateTime.UtcNow).TotalHours,
            RequestedBy = "admin",
            Reason = "Profesional dado de baja / ya no disponible en la plataforma",
            RefundTier = RefundTier.Full100,
            RefundAmount = appointment.Amount
        };
        _db.CancellationRequests.Add(cancellationRequest);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(appointment.GoogleEventId))
        {
            await _googleCalendar.CancelSessionEventAsync(appointment.GoogleEventId);
        }

        await TrySendNotificationAsync(
            () => _notifications.SendCancellationRefundNoticeAsync(appointment, appointment.Professional, cancellationRequest),
            appointment.ProfessionalId);

        TempData["SuccessMessage"] = "Cita cancelada con reembolso completo. Se avisó al paciente por correo, ofreciéndole reagendar con otro profesional o esperar el reembolso.";
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
            TempData["ErrorMessage"] = "No se puede eliminar: este profesional tiene citas asociadas (incluso canceladas, para conservar el historial de pagos/reembolsos). Cancela las citas pendientes desde su ficha y luego usa \"Desactivar\" en vez de eliminar.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Professionals.Remove(professional);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Profesional eliminado.";
        return RedirectToAction(nameof(Index));
    }
}
