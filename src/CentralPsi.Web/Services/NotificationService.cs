using System.Globalization;
using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Options;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Services;

public class NotificationService : INotificationService
{
    private readonly IEmailService _email;
    private readonly ITimeZoneService _timeZone;
    private readonly AppOptions _options;
    private static readonly CultureInfo Es = new("es-CL");

    public NotificationService(IEmailService email, ITimeZoneService timeZone, IOptions<AppOptions> options)
    {
        _email = email;
        _timeZone = timeZone;
        _options = options.Value;
    }

    public async Task SendProfessionalVerifiedAsync(Professional professional)
    {
        var body = $@"
            <h2>¡Tu certificado fue validado!</h2>
            <p>Hola {professional.FullName},</p>
            <p>Tu certificado del Ministerio de Salud fue validado correctamente en la Superintendencia de Salud.
               Ya apareces en nuestro listado público de <strong>Nuestros Profesionales</strong> y las personas
               pueden agendar horas contigo a través de CentralPsi.</p>
            <p>Ingresa a tu correo periódicamente para revisar nuevas reservas.</p>
            <p>Equipo CentralPsi</p>";

        await _email.SendAsync(professional.Email, professional.FullName,
            "CentralPsi - Tu perfil profesional fue validado", body);

        var adminBody = $@"
            <h2>Nuevo profesional validado</h2>
            <p><strong>{professional.FullName}</strong> ({professional.Email}) fue validado automáticamente
               contra el certificado N° {professional.CertificateValidationCode} de la Superintendencia de Salud
               y ya está publicado en el sitio.</p>";
        await _email.SendAsync(_options.AdminEmail, "Administrador CentralPsi",
            "Nuevo profesional validado en CentralPsi", adminBody);
    }

    public async Task SendProfessionalRejectedAsync(Professional professional, string reason)
    {
        var body = $@"
            <h2>No pudimos validar tu certificado</h2>
            <p>Hola {professional.FullName},</p>
            <p>No fue posible validar automáticamente tu certificado contra la Superintendencia de Salud.</p>
            <p><strong>Motivo:</strong> {reason}</p>
            <p>Nuestro equipo revisará tu caso manualmente. Si crees que se trata de un error, responde este
               correo adjuntando nuevamente tu certificado.</p>
            <p>Equipo CentralPsi</p>";
        await _email.SendAsync(professional.Email, professional.FullName,
            "CentralPsi - No pudimos validar tu certificado", body);

        var adminBody = $@"
            <h2>Profesional pendiente de revisión manual</h2>
            <p><strong>{professional.FullName}</strong> ({professional.Email}) quedó con estado
               <em>{professional.Status}</em>. Motivo: {reason}. Revísalo en el panel de administración.</p>";
        await _email.SendAsync(_options.AdminEmail, "Administrador CentralPsi",
            "Profesional pendiente de revisión manual", adminBody);
    }

    public async Task SendAppointmentConfirmedAsync(Appointment appointment, Professional professional)
    {
        var startLocal = _timeZone.ToLocal(appointment.ScheduledStartUtc);
        var when = startLocal.ToString("dddd d 'de' MMMM 'de' yyyy, HH:mm 'hrs'", Es);
        var meetLine = string.IsNullOrEmpty(appointment.GoogleMeetLink)
            ? "<p>El enlace de Google Meet será enviado a la brevedad.</p>"
            : $@"<p><a href=""{appointment.GoogleMeetLink}"">{appointment.GoogleMeetLink}</a></p>";
        var cancelLink = $"{_options.BaseUrl}/reserva/cancelar/{appointment.CancellationToken}";

        var patientBody = $@"
            <h2>Tu hora fue confirmada</h2>
            <p>Hola {appointment.PatientFullName},</p>
            <p>Tu sesión con <strong>{professional.FullName}</strong> quedó confirmada para el
               <strong>{when}</strong> (hora de Chile).</p>
            <p><strong>Enlace de la sesión (Google Meet):</strong></p>
            {meetLine}
            <p>Recuerda: CentralPsi solo actúa como agendador (box virtual) entre tú y el profesional; revisa
               nuestras <a href=""{_options.BaseUrl}/terminos"">condiciones de pago y reembolso</a>.</p>
            <p>Si necesitas cancelar, puedes hacerlo aquí: <a href=""{cancelLink}"">{cancelLink}</a></p>
            <p>Equipo CentralPsi</p>";
        await _email.SendAsync(appointment.PatientEmail, appointment.PatientFullName,
            "CentralPsi - Confirmación de tu hora", patientBody);

        var professionalBody = $@"
            <h2>Nueva sesión agendada y pagada</h2>
            <p>Hola {professional.FullName},</p>
            <p>Tienes una nueva sesión el <strong>{when}</strong> (hora de Chile) con {appointment.PatientFullName}.</p>
            {meetLine}
            <p>Equipo CentralPsi</p>";
        await _email.SendAsync(professional.Email, professional.FullName,
            "CentralPsi - Nueva sesión confirmada", professionalBody);
    }

    public async Task SendAttendanceConfirmationRequestAsync(Appointment appointment, Professional professional)
    {
        var patientLink = $"{_options.BaseUrl}/reserva/confirmar-asistencia/{appointment.PatientAttendanceToken}";
        var patientBody = $@"
            <h2>¿Se realizó tu sesión?</h2>
            <p>Hola {appointment.PatientFullName}, para efectos de respaldo del pago realizado, ayúdanos
               confirmando si tu sesión con {professional.FullName} se realizó con normalidad:</p>
            <p><a href=""{patientLink}"">{patientLink}</a></p>";
        await _email.SendAsync(appointment.PatientEmail, appointment.PatientFullName,
            "CentralPsi - Confirma si tu sesión se realizó", patientBody);

        var professionalLink = $"{_options.BaseUrl}/reserva/confirmar-asistencia/{appointment.ProfessionalAttendanceToken}";
        var professionalBody = $@"
            <h2>¿Se realizó la sesión?</h2>
            <p>Hola {professional.FullName}, para efectos de respaldo del pago, confírmanos si la sesión con
               {appointment.PatientFullName} se realizó con normalidad:</p>
            <p><a href=""{professionalLink}"">{professionalLink}</a></p>";
        await _email.SendAsync(professional.Email, professional.FullName,
            "CentralPsi - Confirma si la sesión se realizó", professionalBody);
    }

    public async Task SendCancellationRefundNoticeAsync(Appointment appointment, Professional professional, CancellationRequest request)
    {
        var startLocal = _timeZone.ToLocal(appointment.ScheduledStartUtc);
        var when = startLocal.ToString("dddd d 'de' MMMM 'de' yyyy, HH:mm 'hrs'", Es);

        var refundBody = $@"
            <h2>Solicitud de reembolso - cita cancelada</h2>
            <p><strong>Cita:</strong> {appointment.Id}</p>
            <p><strong>Paciente:</strong> {appointment.PatientFullName} ({appointment.PatientEmail}, {appointment.PatientPhone})</p>
            <p><strong>Profesional:</strong> {professional.FullName}</p>
            <p><strong>Hora agendada:</strong> {when} (hora de Chile)</p>
            <p><strong>Cancelada por:</strong> {request.RequestedBy}</p>
            <p><strong>Horas de anticipación:</strong> {request.HoursBeforeAppointment:F1}</p>
            <p><strong>Monto pagado:</strong> ${appointment.Amount:N0} CLP</p>
            <p><strong>Nivel de reembolso calculado:</strong> {request.RefundTier} - monto sugerido ${request.RefundAmount:N0} CLP</p>
            <p><strong>Motivo indicado:</strong> {request.Reason}</p>
            <p>Este reembolso debe procesarse manualmente (transferencia) dentro de un máximo de
               {_options.RefundProcessingBusinessDays} días hábiles.</p>";
        await _email.SendAsync(_options.RefundsEmail, "Equipo de Reembolsos CentralPsi",
            $"Reembolso a procesar - cita {appointment.Id}", refundBody);

        var patientBody = $@"
            <h2>Tu hora fue cancelada</h2>
            <p>Hola {appointment.PatientFullName}, confirmamos la cancelación de tu hora del {when} con
               {professional.FullName}.</p>
            <p>Según nuestra política de reembolsos, corresponde un reembolso de
               <strong>${request.RefundAmount:N0} CLP</strong> ({request.RefundTier}), el cual será procesado de
               forma manual dentro de un máximo de {_options.RefundProcessingBusinessDays} días hábiles a la
               cuenta que nos indiques respondiendo este correo.</p>
            <p>Equipo CentralPsi</p>";
        await _email.SendAsync(appointment.PatientEmail, appointment.PatientFullName,
            "CentralPsi - Confirmación de cancelación", patientBody);
    }
}
