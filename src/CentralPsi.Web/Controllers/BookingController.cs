using CentralPsi.Web.Data;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Models.ViewModels;
using CentralPsi.Web.Options;
using CentralPsi.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Controllers;

[Route("reserva")]
public class BookingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ITimeZoneService _timeZoneService;
    private readonly IPaymentService _paymentService;
    private readonly IGoogleCalendarService _googleCalendar;
    private readonly INotificationService _notifications;
    private readonly IRefundCalculationService _refundCalculation;
    private readonly AppOptions _appOptions;
    private readonly ILogger<BookingController> _logger;

    public BookingController(
        ApplicationDbContext db,
        ITimeZoneService timeZoneService,
        IPaymentService paymentService,
        IGoogleCalendarService googleCalendar,
        INotificationService notifications,
        IRefundCalculationService refundCalculation,
        IOptions<AppOptions> appOptions,
        ILogger<BookingController> logger)
    {
        _db = db;
        _timeZoneService = timeZoneService;
        _paymentService = paymentService;
        _googleCalendar = googleCalendar;
        _notifications = notifications;
        _refundCalculation = refundCalculation;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    [HttpGet("nueva")]
    public async Task<IActionResult> Start(Guid professionalId, DateTime startUtc)
    {
        var professional = await _db.Professionals
            .FirstOrDefaultAsync(p => p.Id == professionalId && p.Status == ProfessionalStatus.Verified);
        if (professional is null) return NotFound();

        startUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        if (startUtc <= DateTime.UtcNow)
        {
            TempData["ErrorMessage"] = "Ese horario ya no está disponible, elige otro.";
            return RedirectToAction("Details", "Professionals", new { id = professionalId });
        }

        var taken = await _db.Appointments.AnyAsync(a =>
            a.ProfessionalId == professionalId &&
            a.ScheduledStartUtc == startUtc &&
            (a.Status == AppointmentStatus.PendingPayment || a.Status == AppointmentStatus.Confirmed));
        if (taken)
        {
            TempData["ErrorMessage"] = "Ese horario ya fue reservado por otra persona, elige otro.";
            return RedirectToAction("Details", "Professionals", new { id = professionalId });
        }

        var vm = new BookingStartViewModel
        {
            ProfessionalId = professionalId,
            ProfessionalName = professional.FullName,
            StartUtc = startUtc,
            StartLocal = _timeZoneService.ToLocal(startUtc),
            Amount = _appOptions.AppointmentPriceClp
        };
        return View(vm);
    }

    [HttpPost("nueva")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(BookingStartViewModel model)
    {
        var professional = await _db.Professionals
            .FirstOrDefaultAsync(p => p.Id == model.ProfessionalId && p.Status == ProfessionalStatus.Verified);
        if (professional is null) return NotFound();

        model.StartUtc = DateTime.SpecifyKind(model.StartUtc, DateTimeKind.Utc);
        model.ProfessionalName = professional.FullName;
        model.StartLocal = _timeZoneService.ToLocal(model.StartUtc);
        model.Amount = _appOptions.AppointmentPriceClp;

        var taken = await _db.Appointments.AnyAsync(a =>
            a.ProfessionalId == model.ProfessionalId &&
            a.ScheduledStartUtc == model.StartUtc &&
            (a.Status == AppointmentStatus.PendingPayment || a.Status == AppointmentStatus.Confirmed));
        if (taken)
        {
            ModelState.AddModelError(string.Empty, "Ese horario ya fue reservado por otra persona, vuelve a elegir un horario.");
        }

        if (!model.TermsAccepted)
        {
            ModelState.AddModelError(nameof(model.TermsAccepted), "Debes aceptar los términos y condiciones para continuar");
        }

        if (model.IsForMinor)
        {
            if (string.IsNullOrWhiteSpace(model.MinorFullName))
            {
                ModelState.AddModelError(nameof(model.MinorFullName), "Ingresa el nombre completo del niño, niña o adolescente");
            }
            if (model.MinorAge is null)
            {
                ModelState.AddModelError(nameof(model.MinorAge), "Ingresa la edad");
            }
            if (string.IsNullOrWhiteSpace(model.GuardianRelationship))
            {
                ModelState.AddModelError(nameof(model.GuardianRelationship), "Indica tu relación con el niño, niña o adolescente");
            }
            if (!model.GuardianConsentAccepted)
            {
                ModelState.AddModelError(nameof(model.GuardianConsentAccepted), "Debes confirmar que eres madre, padre o tutor/a legal y autorizas la sesión");
            }
        }

        if (!ModelState.IsValid)
        {
            return View("Start", model);
        }

        var appointment = new Appointment
        {
            ProfessionalId = professional.Id,
            PatientFullName = model.PatientFullName.Trim(),
            PatientEmail = model.PatientEmail.Trim().ToLowerInvariant(),
            PatientPhone = model.PatientPhone.Trim(),
            ScheduledStartUtc = model.StartUtc,
            ScheduledEndUtc = model.StartUtc.AddMinutes(_appOptions.SessionDurationMinutes),
            Amount = _appOptions.AppointmentPriceClp,
            TermsAccepted = true,
            TermsAcceptedAtUtc = DateTime.UtcNow,
            TermsAcceptedIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            IsForMinor = model.IsForMinor,
            MinorFullName = model.IsForMinor ? model.MinorFullName!.Trim() : null,
            MinorAge = model.IsForMinor ? model.MinorAge : null,
            GuardianRelationship = model.IsForMinor ? model.GuardianRelationship!.Trim() : null,
            GuardianConsentAcceptedAtUtc = model.IsForMinor ? DateTime.UtcNow : null
        };
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        var returnUrl = Url.Action(nameof(PaymentReturn), "Booking", null, Request.Scheme)!;
        var buyOrder = appointment.Id.ToString("N")[..24];
        var sessionId = appointment.Id.ToString("N");

        try
        {
            var created = await _paymentService.CreateTransactionAsync(buyOrder, sessionId, appointment.Amount, returnUrl);
            _db.Payments.Add(new Payment
            {
                AppointmentId = appointment.Id,
                BuyOrder = buyOrder,
                SessionId = sessionId,
                Token = created.Token,
                Amount = appointment.Amount,
                Status = PaymentStatus.Initiated
            });
            await _db.SaveChangesAsync();

            return View("Redirect", new WebpayRedirectViewModel { Token = created.Token, RedirectUrl = created.RedirectUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando transacción Webpay para la cita {AppointmentId}", appointment.Id);
            TempData["ErrorMessage"] = "No pudimos iniciar el pago en este momento. Intenta nuevamente en unos minutos.";
            return RedirectToAction("Details", "Professionals", new { id = professional.Id });
        }
    }

    [HttpPost("retorno-pago")]
    [HttpGet("retorno-pago")]
    public async Task<IActionResult> PaymentReturn(string? token_ws, string? TBK_TOKEN)
    {
        if (string.IsNullOrEmpty(token_ws))
        {
            // TBK_TOKEN present (or nothing) means the user cancelled/aborted on Transbank's page.
            ViewData["Aborted"] = true;
            return View("PaymentResult");
        }

        var payment = await _db.Payments.Include(p => p.Appointment)
            .ThenInclude(a => a!.Professional)
            .FirstOrDefaultAsync(p => p.Token == token_ws);
        if (payment?.Appointment is null) return NotFound();

        var appointment = payment.Appointment;
        var professional = appointment.Professional!;

        var commit = await _paymentService.CommitTransactionAsync(token_ws);
        payment.Status = commit.IsApproved ? PaymentStatus.Authorized : PaymentStatus.Failed;
        payment.AuthorizationCode = commit.AuthorizationCode;
        payment.ResponseCode = commit.ResponseCode;
        payment.TransactionDateUtc = commit.TransactionDateUtc?.ToUniversalTime();
        payment.RawResponseJson = commit.RawJson;

        if (commit.IsApproved)
        {
            appointment.Status = AppointmentStatus.Confirmed;

            var meet = await _googleCalendar.CreateSessionEventAsync(appointment, professional);
            appointment.GoogleEventId = meet.EventId;
            appointment.GoogleMeetLink = meet.MeetLink;

            await _db.SaveChangesAsync();

            try
            {
                await _notifications.SendAppointmentConfirmedAsync(appointment, professional);
            }
            catch (Exception ex)
            {
                // The payment is already authorized and saved above - a notification failure (bad SMTP
                // credentials, provider outage, etc.) must not turn into a 500 that makes a paying patient
                // think their booking failed.
                _logger.LogError(ex, "Error enviando el correo de confirmación para la cita {AppointmentId}", appointment.Id);
            }
        }
        else
        {
            await _db.SaveChangesAsync();
        }

        ViewData["Aborted"] = false;
        ViewData["Approved"] = commit.IsApproved;
        ViewData["Appointment"] = appointment;
        ViewData["Professional"] = professional;
        return View("PaymentResult");
    }

    [HttpGet("cancelar/{token}")]
    public async Task<IActionResult> Cancel(string token)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Professional)
            .Include(a => a.CancellationRequest)
            .FirstOrDefaultAsync(a => a.CancellationToken == token);
        if (appointment?.Professional is null) return NotFound();

        var (tier, amount, hoursBefore) = _refundCalculation.Calculate(appointment.ScheduledStartUtc, appointment.Amount);
        var vm = new CancellationViewModel
        {
            Appointment = appointment,
            Professional = appointment.Professional,
            StartLocal = _timeZoneService.ToLocal(appointment.ScheduledStartUtc),
            ProjectedTier = tier,
            ProjectedAmount = amount,
            HoursBefore = hoursBefore,
            AlreadyCancelled = appointment.Status == AppointmentStatus.Cancelled
        };
        return View(vm);
    }

    [HttpPost("cancelar/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelConfirm(string token, string? reason)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Professional)
            .FirstOrDefaultAsync(a => a.CancellationToken == token);
        if (appointment?.Professional is null) return NotFound();

        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            TempData["SuccessMessage"] = "Esta cita ya se encontraba cancelada.";
            return RedirectToAction(nameof(Cancel), new { token });
        }

        var (tier, amount, hoursBefore) = _refundCalculation.Calculate(appointment.ScheduledStartUtc, appointment.Amount);

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAtUtc = DateTime.UtcNow;
        appointment.CancelledBy = "patient";

        var cancellationRequest = new CancellationRequest
        {
            AppointmentId = appointment.Id,
            HoursBeforeAppointment = hoursBefore,
            RequestedBy = "patient",
            Reason = reason,
            RefundTier = tier,
            RefundAmount = amount
        };
        _db.CancellationRequests.Add(cancellationRequest);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(appointment.GoogleEventId))
        {
            await _googleCalendar.CancelSessionEventAsync(appointment.GoogleEventId);
        }

        try
        {
            await _notifications.SendCancellationRefundNoticeAsync(appointment, appointment.Professional, cancellationRequest);
        }
        catch (Exception ex)
        {
            // The cancellation itself is already saved above - don't fail the whole request over a
            // notification-only error, but log it since reembolsos@ needs that email to process the refund.
            _logger.LogError(ex, "Error enviando el aviso de cancelación/reembolso para la cita {AppointmentId}", appointment.Id);
        }

        TempData["SuccessMessage"] = "Tu hora fue cancelada. Revisa tu correo para conocer el detalle del reembolso.";
        return RedirectToAction(nameof(Cancel), new { token });
    }

    [HttpGet("confirmar-asistencia/{token}")]
    public async Task<IActionResult> ConfirmAttendance(string token)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Professional)
            .FirstOrDefaultAsync(a => a.PatientAttendanceToken == token || a.ProfessionalAttendanceToken == token);
        if (appointment?.Professional is null) return NotFound();

        var isPatient = appointment.PatientAttendanceToken == token;
        var vm = new AttendanceConfirmationViewModel
        {
            Appointment = appointment,
            Professional = appointment.Professional,
            Role = isPatient ? "paciente" : "profesional",
            AlreadyAnswered = isPatient ? appointment.PatientAttendanceConfirmedAtUtc.HasValue : appointment.ProfessionalAttendanceConfirmedAtUtc.HasValue
        };
        return View(vm);
    }

    [HttpPost("confirmar-asistencia/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmAttendanceSubmit(string token, bool sessionHappened)
    {
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.PatientAttendanceToken == token || a.ProfessionalAttendanceToken == token);
        if (appointment is null) return NotFound();

        if (appointment.PatientAttendanceToken == token)
        {
            appointment.PatientConfirmsSessionHappened = sessionHappened;
            appointment.PatientAttendanceConfirmedAtUtc = DateTime.UtcNow;
        }
        else
        {
            appointment.ProfessionalConfirmsSessionHappened = sessionHappened;
            appointment.ProfessionalAttendanceConfirmedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "¡Gracias por confirmar! Esto nos ayuda a respaldar el pago de la sesión.";
        return RedirectToAction(nameof(ConfirmAttendance), new { token });
    }
}
