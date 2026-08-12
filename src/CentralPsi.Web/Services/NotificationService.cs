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

    /// <summary>Wraps a piece of message-specific HTML in the shared CentralPsi branded email shell (logo
    /// header, card body, footer with contact links) - uses a table layout and inline styles throughout since
    /// that's what renders consistently across email clients (Outlook in particular ignores most CSS).</summary>
    private string Wrap(string innerHtml)
    {
        var logoUrl = $"{_options.BaseUrl}/images/logo-email.png";
        return $@"
<!DOCTYPE html>
<html lang=""es"">
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background-color:#f2f6f5;font-family:Arial,Helvetica,sans-serif;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f2f6f5;padding:32px 16px;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff;max-width:600px;width:100%;border-radius:12px;overflow:hidden;border:1px solid #e1eae8;"">
          <tr>
            <td style=""padding:24px 32px;border-bottom:1px solid #e1eae8;"">
              <img src=""{logoUrl}"" alt=""CentralPsi"" height=""30"" style=""display:block;border:0;"" />
            </td>
          </tr>
          <tr>
            <td style=""padding:32px;color:#24312f;font-size:15px;line-height:1.65;"">
              {innerHtml}
            </td>
          </tr>
          <tr>
            <td style=""background-color:#204e4a;padding:22px 32px;font-size:12px;color:#c6ddda;"">
              <p style=""margin:0 0 6px;"">CentralPsi es un espacio de agendamiento online (box virtual) entre
                 pacientes y profesionales de la psicología. No presta servicios de salud ni almacena
                 antecedentes clínicos.</p>
              <p style=""margin:0;"">
                Consultas: <a href=""mailto:{_options.AdminEmail}"" style=""color:#9fd6cc;"">{_options.AdminEmail}</a>
                &nbsp;·&nbsp;
                Reembolsos: <a href=""mailto:{_options.RefundsEmail}"" style=""color:#9fd6cc;"">{_options.RefundsEmail}</a>
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    private static string Heading(string text) =>
        $@"<h2 style=""margin:0 0 16px;color:#204e4a;font-size:21px;"">{text}</h2>";

    private static string Paragraph(string html) =>
        $@"<p style=""margin:0 0 16px;"">{html}</p>";

    private static string Button(string url, string label) => $@"
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:8px 0 20px;"">
          <tr>
            <td style=""background-color:#2f6f6a;border-radius:999px;"">
              <a href=""{url}"" style=""display:inline-block;padding:12px 26px;color:#ffffff;text-decoration:none;font-weight:bold;font-size:14px;"">{label}</a>
            </td>
          </tr>
        </table>";

    private static string InfoBox(string html) => $@"
        <div style=""background-color:#eef6f5;border-radius:8px;padding:16px 18px;margin:0 0 20px;font-size:14px;color:#204e4a;"">
          {html}
        </div>";

    private static string SessionTipsBox(bool forProfessional)
    {
        var items = forProfessional
            ? new[]
            {
                "Conéctate desde un espacio <strong>privado y silencioso</strong>, que resguarde la confidencialidad de la sesión.",
                "Verifica tu <strong>conexión a internet, cámara y micrófono</strong> antes de la hora.",
                "Ten el enlace de la sesión a mano con anticipación.",
                "Evita interrupciones y notificaciones durante la sesión."
            }
            : new[]
            {
                "Busca un lugar <strong>silencioso y privado</strong>, sin otras personas alrededor.",
                "Verifica que tengas <strong>buena iluminación</strong>, así el profesional puede verte con claridad.",
                "Confirma que tu <strong>conexión a internet sea estable</strong> (evita usar solo datos móviles si puedes).",
                "Prueba tu <strong>cámara y micrófono</strong> antes de la hora agendada.",
                "Si no puedes estar en un lugar 100% privado, usar <strong>audífonos</strong> ayuda a resguardar tu confidencialidad."
            };
        var listItems = string.Join("", items.Select(i => $@"<li style=""margin-bottom:6px;"">{i}</li>"));
        return InfoBox($@"
          <p style=""margin:0 0 8px;font-weight:bold;"">Antes de tu sesión</p>
          <ul style=""margin:0;padding-left:18px;"">{listItems}</ul>");
    }

    public async Task SendProfessionalVerifiedAsync(Professional professional)
    {
        var body = Wrap(
            Heading("¡Tu certificado fue validado!") +
            Paragraph($"Hola {professional.FullName},") +
            Paragraph(@"Tu certificado del Ministerio de Salud fue validado correctamente en la
                Superintendencia de Salud. Ya apareces en nuestro listado público de
                <strong>Nuestros Profesionales</strong> y las personas pueden agendar horas contigo a través
                de CentralPsi.") +
            Paragraph("Ingresa a tu correo periódicamente para revisar nuevas reservas.") +
            Paragraph("Equipo CentralPsi"));

        await _email.SendAsync(professional.Email, professional.FullName,
            "CentralPsi - Tu perfil profesional fue validado", body);

        var adminBody = Wrap(
            Heading("Nuevo profesional validado") +
            Paragraph($@"<strong>{professional.FullName}</strong> ({professional.Email}) fue validado
                automáticamente contra el certificado N° {professional.CertificateValidationCode} de la
                Superintendencia de Salud y ya está publicado en el sitio."));
        await _email.SendAsync(_options.AdminEmail, "Administrador CentralPsi",
            "Nuevo profesional validado en CentralPsi", adminBody);
    }

    public async Task SendProfessionalRejectedAsync(Professional professional, string reason)
    {
        var body = Wrap(
            Heading("No pudimos validar tu certificado") +
            Paragraph($"Hola {professional.FullName},") +
            Paragraph("No fue posible validar automáticamente tu certificado contra la Superintendencia de Salud.") +
            InfoBox($"<strong>Motivo:</strong> {reason}") +
            Paragraph(@"Nuestro equipo revisará tu caso manualmente. Si crees que se trata de un error,
                responde este correo adjuntando nuevamente tu certificado.") +
            Paragraph("Equipo CentralPsi"));
        await _email.SendAsync(professional.Email, professional.FullName,
            "CentralPsi - No pudimos validar tu certificado", body);

        var adminBody = Wrap(
            Heading("Profesional pendiente de revisión manual") +
            Paragraph($@"<strong>{professional.FullName}</strong> ({professional.Email}) quedó con estado
                <em>{professional.Status}</em>. Motivo: {reason}. Revísalo en el panel de administración."));
        await _email.SendAsync(_options.AdminEmail, "Administrador CentralPsi",
            "Profesional pendiente de revisión manual", adminBody);
    }

    public async Task SendAppointmentConfirmedAsync(Appointment appointment, Professional professional)
    {
        var startLocal = _timeZone.ToLocal(appointment.ScheduledStartUtc);
        var when = startLocal.ToString("dddd d 'de' MMMM 'de' yyyy, HH:mm 'hrs'", Es);
        var meetSection = string.IsNullOrEmpty(appointment.GoogleMeetLink)
            ? Paragraph("El enlace de Google Meet será enviado a la brevedad.")
            : Button(appointment.GoogleMeetLink, "Unirme a la sesión (Google Meet)");
        var cancelLink = $"{_options.BaseUrl}/reserva/cancelar/{appointment.CancellationToken}";

        var patientBody = Wrap(
            Heading("Tu hora fue confirmada") +
            Paragraph($"Hola {appointment.PatientFullName},") +
            Paragraph($@"Tu sesión con <strong>{professional.FullName}</strong> quedó confirmada para el
                <strong>{when}</strong> (hora de Chile).") +
            meetSection +
            SessionTipsBox(forProfessional: false) +
            Paragraph($@"Recuerda: CentralPsi solo actúa como agendador (box virtual) entre tú y el
                profesional; revisa nuestras <a href=""{_options.BaseUrl}/terminos"" style=""color:#2f6f6a;"">
                condiciones de pago y reembolso</a>.") +
            Paragraph($@"¿Necesitas cancelar? <a href=""{cancelLink}"" style=""color:#2f6f6a;"">Cancela tu hora aquí</a>.") +
            Paragraph("Equipo CentralPsi"));
        await _email.SendAsync(appointment.PatientEmail, appointment.PatientFullName,
            "CentralPsi - Confirmación de tu hora", patientBody);

        var professionalBody = Wrap(
            Heading("Nueva sesión agendada y pagada") +
            Paragraph($"Hola {professional.FullName},") +
            Paragraph($@"Tienes una nueva sesión el <strong>{when}</strong> (hora de Chile) con
                {appointment.PatientFullName}.") +
            meetSection +
            SessionTipsBox(forProfessional: true) +
            Paragraph("Equipo CentralPsi"));
        await _email.SendAsync(professional.Email, professional.FullName,
            "CentralPsi - Nueva sesión confirmada", professionalBody);
    }

    public async Task SendAttendanceConfirmationRequestAsync(Appointment appointment, Professional professional)
    {
        var patientLink = $"{_options.BaseUrl}/reserva/confirmar-asistencia/{appointment.PatientAttendanceToken}";
        var patientBody = Wrap(
            Heading("¿Se realizó tu sesión?") +
            Paragraph($@"Hola {appointment.PatientFullName}, para efectos de respaldo del pago realizado,
                ayúdanos confirmando si tu sesión con {professional.FullName} se realizó con normalidad:") +
            Button(patientLink, "Confirmar asistencia"));
        await _email.SendAsync(appointment.PatientEmail, appointment.PatientFullName,
            "CentralPsi - Confirma si tu sesión se realizó", patientBody);

        var professionalLink = $"{_options.BaseUrl}/reserva/confirmar-asistencia/{appointment.ProfessionalAttendanceToken}";
        var professionalBody = Wrap(
            Heading("¿Se realizó la sesión?") +
            Paragraph($@"Hola {professional.FullName}, para efectos de respaldo del pago, confírmanos si la
                sesión con {appointment.PatientFullName} se realizó con normalidad:") +
            Button(professionalLink, "Confirmar asistencia"));
        await _email.SendAsync(professional.Email, professional.FullName,
            "CentralPsi - Confirma si la sesión se realizó", professionalBody);
    }

    public async Task SendCancellationRefundNoticeAsync(Appointment appointment, Professional professional, CancellationRequest request)
    {
        var startLocal = _timeZone.ToLocal(appointment.ScheduledStartUtc);
        var when = startLocal.ToString("dddd d 'de' MMMM 'de' yyyy, HH:mm 'hrs'", Es);

        var refundBody = Wrap(
            Heading("Solicitud de reembolso - cita cancelada") +
            InfoBox($@"
                <p style=""margin:0 0 6px;""><strong>Cita:</strong> {appointment.Id}</p>
                <p style=""margin:0 0 6px;""><strong>Paciente:</strong> {appointment.PatientFullName} ({appointment.PatientEmail}, {appointment.PatientPhone})</p>
                <p style=""margin:0 0 6px;""><strong>Profesional:</strong> {professional.FullName}</p>
                <p style=""margin:0 0 6px;""><strong>Hora agendada:</strong> {when} (hora de Chile)</p>
                <p style=""margin:0 0 6px;""><strong>Cancelada por:</strong> {request.RequestedBy}</p>
                <p style=""margin:0 0 6px;""><strong>Horas de anticipación:</strong> {request.HoursBeforeAppointment:F1}</p>
                <p style=""margin:0 0 6px;""><strong>Monto pagado:</strong> ${appointment.Amount:N0} CLP</p>
                <p style=""margin:0 0 6px;""><strong>Nivel de reembolso calculado:</strong> {request.RefundTier} - monto sugerido ${request.RefundAmount:N0} CLP</p>
                <p style=""margin:0;""><strong>Motivo indicado:</strong> {request.Reason}</p>") +
            Paragraph($@"Este reembolso debe procesarse manualmente (transferencia) dentro de un máximo de
                {_options.RefundProcessingBusinessDays} días hábiles."));
        await _email.SendAsync(_options.RefundsEmail, "Equipo de Reembolsos CentralPsi",
            $"Reembolso a procesar - cita {appointment.Id}", refundBody);

        var patientBody = Wrap(
            Heading("Tu hora fue cancelada") +
            Paragraph($@"Hola {appointment.PatientFullName}, confirmamos la cancelación de tu hora del
                {when} con {professional.FullName}.") +
            Paragraph($@"Según nuestra política de reembolsos, corresponde un reembolso de
                <strong>${request.RefundAmount:N0} CLP</strong> ({request.RefundTier}), el cual será
                procesado de forma manual dentro de un máximo de {_options.RefundProcessingBusinessDays} días
                hábiles a la cuenta que nos indiques respondiendo este correo.") +
            Paragraph("Equipo CentralPsi"));
        await _email.SendAsync(appointment.PatientEmail, appointment.PatientFullName,
            "CentralPsi - Confirmación de cancelación", patientBody);
    }
}
