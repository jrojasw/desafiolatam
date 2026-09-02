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
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<ProfessionalsController> _logger;

    public ProfessionalsController(
        ApplicationDbContext db,
        IFileStorageService fileStorage,
        INotificationService notifications,
        ICertificateValidationService certificateValidation,
        IGoogleCalendarService googleCalendar,
        IAuditLogService auditLog,
        ILogger<ProfessionalsController> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _notifications = notifications;
        _certificateValidation = certificateValidation;
        _googleCalendar = googleCalendar;
        _auditLog = auditLog;
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
        ViewBag.PendingFonasaCount = await _db.Professionals
            .CountAsync(p => p.Status == ProfessionalStatus.Verified && p.FonasaConfirmedAtUtc == null);
        var professionals = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return View(professionals);
    }

    /// <summary>
    /// Bulk-sends the "confirm your Fonasa status" email to every verified professional who hasn't answered
    /// yet (registered before the Fonasa field existed, so it currently defaults to "No"). Skips anyone who
    /// already confirmed - either through this same email, or by answering the question when they registered.
    /// </summary>
    [HttpPost("EnviarConfirmacionFonasa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendFonasaConfirmationEmails()
    {
        var pending = await _db.Professionals
            .Where(p => p.Status == ProfessionalStatus.Verified && p.FonasaConfirmedAtUtc == null)
            .ToListAsync();

        var sent = 0;
        foreach (var professional in pending)
        {
            professional.FonasaConfirmationToken = Guid.NewGuid().ToString("N");
            professional.FonasaConfirmationSentAtUtc = DateTime.UtcNow;
            await TrySendNotificationAsync(() => _notifications.SendFonasaConfirmationRequestAsync(professional), professional.Id);
            sent++;
        }
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = sent == 0
            ? "No hay profesionales pendientes de confirmar su situación en Fonasa."
            : $"Se envió el correo de confirmación a {sent} profesional(es).";
        return RedirectToAction(nameof(Index));
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

        await _auditLog.LogAsync("Ver documento privado", "Professional", id.ToString(), $"Tipo: {type}");

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

    private static readonly HashSet<string> AllowedPhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    [HttpPost("{id:guid}/Foto")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfilePhoto(Guid id, IFormFile? photo)
    {
        var professional = await _db.Professionals.FindAsync(id);
        if (professional is null) return NotFound();

        if (photo is null || photo.Length == 0)
        {
            TempData["ErrorMessage"] = "Selecciona una imagen antes de subir.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!AllowedPhotoExtensions.Contains(Path.GetExtension(photo.FileName)))
        {
            TempData["ErrorMessage"] = "Formato no permitido. Usa una imagen JPG, PNG o WEBP.";
            return RedirectToAction(nameof(Details), new { id });
        }

        professional.ProfilePhotoPath = await _fileStorage.SavePublicAsync(photo, "profesionales");
        await _db.SaveChangesAsync();
        await _auditLog.LogAsync("Reemplazar foto de perfil", "Professional", id.ToString(), professional.FullName);

        TempData["SuccessMessage"] = $"Foto de perfil de {professional.FullName} actualizada.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/QuitarFoto")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProfilePhoto(Guid id)
    {
        var professional = await _db.Professionals.FindAsync(id);
        if (professional is null) return NotFound();

        professional.ProfilePhotoPath = null;
        await _db.SaveChangesAsync();
        await _auditLog.LogAsync("Quitar foto de perfil", "Professional", id.ToString(), professional.FullName);

        TempData["SuccessMessage"] = $"Se quitó la foto de {professional.FullName}; ahora se muestra la foto genérica en su perfil público.";
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

    /// <summary>
    /// Escape hatch for removing test/fictitious profiles that already went through a real booking during
    /// development: normal Delete refuses to touch a professional with any appointment history at all to
    /// protect real payment/refund records, but that guard has no way to distinguish "real booking with money
    /// owed to a real patient/professional" from "fake test booking with fake test data". Only allowed once
    /// every appointment is out of an active/uncommitted state (nothing still PendingPayment or Confirmed) -
    /// this purges those appointment rows (and their cascading Payment/CancellationRequest rows, Completed ones
    /// included) along with the professional, instead of just blocking. Only ever use this on profiles you know
    /// are test data.
    /// </summary>
    [HttpPost("{id:guid}/EliminarForzado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForceDelete(Guid id)
    {
        var professional = await _db.Professionals.FindAsync(id);
        if (professional is null) return NotFound();

        var appointments = await _db.Appointments.Where(a => a.ProfessionalId == id).ToListAsync();
        var hasActive = appointments.Any(a => a.Status is AppointmentStatus.PendingPayment or AppointmentStatus.Confirmed);
        if (hasActive)
        {
            TempData["ErrorMessage"] = "No se puede forzar la eliminación: aún tiene citas activas (pendientes de pago o confirmadas). Cancélalas primero desde la lista de arriba.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Appointments.RemoveRange(appointments);
        _db.Professionals.Remove(professional);
        await _db.SaveChangesAsync();
        await _auditLog.LogAsync("Forzar eliminación", "Professional", id.ToString(), professional.FullName);

        TempData["SuccessMessage"] = $"{professional.FullName} y sus citas fueron eliminados definitivamente.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// "Right to be forgotten" (Ley 21.719): scrubs the professional's personal data and deletes their private
    /// documents, but - unlike ForceDelete - keeps the Professional row and every Appointment intact, since
    /// financial/tax records (amounts, dates, payout status) must be retained for SII purposes even after the
    /// person's identity is erased. Blocked while there's an active appointment, same reasoning as ForceDelete:
    /// erasing the name/contact info out from under a session a patient is still expecting would be worse than
    /// just asking the requester to wait until it's resolved.
    /// </summary>
    [HttpPost("{id:guid}/Anonimizar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anonymize(Guid id)
    {
        var professional = await _db.Professionals.FindAsync(id);
        if (professional is null) return NotFound();

        var hasActive = await _db.Appointments.AnyAsync(a =>
            a.ProfessionalId == id && (a.Status == AppointmentStatus.PendingPayment || a.Status == AppointmentStatus.Confirmed));
        if (hasActive)
        {
            TempData["ErrorMessage"] = "No se puede anonimizar: aún tiene citas activas (pendientes de pago o confirmadas). Resuélvelas primero.";
            return RedirectToAction(nameof(Details), new { id });
        }

        foreach (var path in new[] { professional.CedulaFrontPath, professional.CedulaBackPath, professional.CertificateFilePath, professional.ProfilePhotoPath })
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            try
            {
                var physicalPath = _fileStorage.GetPrivatePhysicalPath(path);
                if (System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo borrar el archivo {Path} al anonimizar al profesional {ProfessionalId}", path, id);
            }
        }

        var anonymizedName = $"Profesional eliminado ({id.ToString()[..8]})";
        professional.FullName = anonymizedName;
        professional.Email = $"eliminado-{id}@centralpsi.cl";
        professional.Phone = string.Empty;
        professional.Rut = null;
        professional.Experience = "Datos eliminados a solicitud del profesional.";
        professional.CedulaFrontPath = string.Empty;
        professional.CedulaBackPath = string.Empty;
        professional.CertificateFilePath = string.Empty;
        professional.ProfilePhotoPath = null;
        professional.CertificateQrRawData = null;
        professional.BankName = string.Empty;
        professional.BankAccountType = string.Empty;
        professional.BankAccountNumber = string.Empty;
        professional.BankAccountHolderName = string.Empty;
        professional.BankAccountHolderRut = string.Empty;
        professional.Status = ProfessionalStatus.Inactive;
        professional.DeactivatedAt ??= DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _auditLog.LogAsync("Anonimizar (derecho al olvido)", "Professional", id.ToString(), "Datos personales eliminados; se conservan citas y montos para el SII.");

        TempData["SuccessMessage"] = $"Datos personales de {anonymizedName} eliminados. Se conservó el historial de citas y pagos (sin datos identificatorios) por obligación tributaria.";
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
            TempData["ErrorMessage"] = "No se puede eliminar: este profesional tiene citas asociadas (incluso canceladas, para conservar el historial de pagos/reembolsos). Cancela las citas pendientes desde su ficha y luego usa \"Desactivar\", o si son datos de prueba/ficticios usa \"Forzar eliminación\" más abajo.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.Professionals.Remove(professional);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Profesional eliminado.";
        return RedirectToAction(nameof(Index));
    }
}
