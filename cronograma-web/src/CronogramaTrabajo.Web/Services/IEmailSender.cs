namespace CronogramaTrabajo.Web.Services;

public interface IEmailSender
{
    Task SendEmailAsync(string destinatario, string asunto, string cuerpoHtml);
}
