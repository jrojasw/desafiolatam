using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace CronogramaTrabajo.Web.Services;

public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly string _fromEmail;

    public ResendEmailSender(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.resend.com/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", configuration["Resend:ApiKey"]);

        _fromEmail = configuration["Resend:FromEmail"]
            ?? throw new InvalidOperationException("Falta configurar Resend:FromEmail.");
    }

    public async Task SendEmailAsync(string destinatario, string asunto, string cuerpoHtml)
    {
        var response = await _httpClient.PostAsJsonAsync("emails", new
        {
            from = _fromEmail,
            to = new[] { destinatario },
            subject = asunto,
            html = cuerpoHtml
        });

        response.EnsureSuccessStatusCode();
    }
}
