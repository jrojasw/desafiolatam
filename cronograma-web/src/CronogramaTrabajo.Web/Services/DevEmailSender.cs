namespace CronogramaTrabajo.Web.Services;

/// <summary>
/// Se usa cuando no hay una API key de Resend configurada (entorno local/desarrollo):
/// en vez de enviar el correo, escribe el enlace en los logs para poder probar el flujo.
/// </summary>
public class DevEmailSender : IEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string destinatario, string asunto, string cuerpoHtml)
    {
        _logger.LogWarning(
            "[DevEmailSender] No hay Resend:ApiKey configurada. Correo simulado para {Destinatario} - {Asunto}:\n{Cuerpo}",
            destinatario, asunto, cuerpoHtml);
        return Task.CompletedTask;
    }
}
