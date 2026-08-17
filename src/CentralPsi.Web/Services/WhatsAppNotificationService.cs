using CentralPsi.Web.Options;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Services;

public class WhatsAppNotificationService : IWhatsAppNotificationService
{
    private readonly WhatsAppOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WhatsAppNotificationService> _logger;

    public WhatsAppNotificationService(IOptions<WhatsAppOptions> options, IHttpClientFactory httpClientFactory, ILogger<WhatsAppNotificationService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Phone) && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task SendAsync(string message, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("WhatsApp no está configurado (WhatsApp:Phone / WhatsApp:ApiKey); se omite el aviso: {Message}", message);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://api.callmebot.com/whatsapp.php?phone={Uri.EscapeDataString(_options.Phone)}" +
                      $"&text={Uri.EscapeDataString(message)}&apikey={Uri.EscapeDataString(_options.ApiKey)}";
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("CallMeBot respondió {StatusCode} al enviar el aviso de WhatsApp: {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando el aviso de WhatsApp");
        }
    }
}
