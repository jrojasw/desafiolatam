using CentralPsi.Web.Data;
using CentralPsi.Web.Data.Seed;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralPsi.Web.Areas.Admin.Controllers;

/// <summary>
/// Ley 21.719 tooling: lets an admin action a patient's "right to be forgotten" request, and shows the
/// append-only audit trail of who accessed sensitive personal data and when.
/// </summary>
[Area("Admin")]
[Authorize(Roles = DataSeeder.AdminRole)]
[Route("Admin/Privacidad")]
public class PrivacyController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _auditLog;

    public PrivacyController(ApplicationDbContext db, IAuditLogService auditLog)
    {
        _db = db;
        _auditLog = auditLog;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.RecentLogs = await _db.AuditLogs
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(100)
            .ToListAsync();
        return View();
    }

    /// <summary>
    /// "Right to be forgotten" for a patient: anonymizes the patient's name/contact info on every appointment
    /// that used that email, but keeps the appointment rows (amounts, dates, statuses) intact - those are
    /// financial/tax records that must be retained for the SII even after the person's identity is scrubbed.
    /// Patients don't have accounts, so email is the only handle we have for "which rows are this person's".
    /// </summary>
    [HttpPost("AnonimizarPaciente")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnonymizePatient(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["ErrorMessage"] = "Ingresa el correo del paciente a anonimizar.";
            return RedirectToAction(nameof(Index));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var appointments = await _db.Appointments
            .Where(a => a.PatientEmail.ToLower() == normalizedEmail)
            .ToListAsync();

        if (appointments.Count == 0)
        {
            TempData["ErrorMessage"] = $"No se encontraron citas con el correo {email}.";
            return RedirectToAction(nameof(Index));
        }

        var hasActive = appointments.Any(a => a.Status is AppointmentStatus.PendingPayment or AppointmentStatus.Confirmed);
        if (hasActive)
        {
            TempData["ErrorMessage"] = "Este paciente tiene una cita pendiente de pago o confirmada. Espera a que se resuelva (o cancélala) antes de anonimizar sus datos.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var appointment in appointments)
        {
            appointment.PatientFullName = "Paciente eliminado";
            appointment.PatientEmail = $"eliminado-{appointment.Id}@centralpsi.cl";
            appointment.PatientPhone = string.Empty;
            if (appointment.IsForMinor)
            {
                appointment.MinorFullName = "Menor eliminado";
            }
        }

        await _db.SaveChangesAsync();
        await _auditLog.LogAsync("Anonimizar paciente (derecho al olvido)", "Appointment",
            string.Join(",", appointments.Select(a => a.Id)),
            $"Correo original: {email} — {appointments.Count} cita(s) afectada(s).");

        TempData["SuccessMessage"] = $"Se anonimizaron los datos personales de {appointments.Count} cita(s). Se conservaron los montos y fechas por obligación tributaria.";
        return RedirectToAction(nameof(Index));
    }
}
