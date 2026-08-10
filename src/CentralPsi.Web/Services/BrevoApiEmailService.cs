using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CentralPsi.Web.Options;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Services;

/// <summary>
/// Sends email through Brevo's transactional email REST API (HTTPS) rather than a raw SMTP connection.
/// Render (and several other PaaS hosts) block outbound SMTP ports by default to curb spam, which shows up as
/// connection timeouts regardless of which mail provider is configured - HTTPS on port 443 doesn't hit that
/// restriction, so this is the reliable path for this hosting setup.
/// </summary>
public class BrevoApiEmailService : IEmailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SmtpOptions _options;
    private readonly ILogger<BrevoApiEmailService> _logger;

    public BrevoApiEmailService(IHttpClientFactory httpClientFactory, IOptions<SmtpOptions> options, ILogger<BrevoApiEmailService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, IEnumerable<string>? cc = null)
    {
        if (_options.DryRun || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogInformation(
                "[DryRun email] To: {To} <{ToEmail}> | CC: {Cc} | Subject: {Subject}\n{Body}",
                toName, toEmail, cc is null ? "" : string.Join(", ", cc), subject, htmlBody);
            return;
        }

        var payload = new
        {
            sender = new { name = _options.FromName, email = _options.FromAddress },
            to = new[] { new { email = toEmail, name = toName } },
            cc = cc?.Select(c => new { email = c }).ToArray(),
            subject,
            htmlContent = htmlBody
        };

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Add("api-key", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Brevo API respondió {(int)response.StatusCode}: {body}");
        }
    }
}
