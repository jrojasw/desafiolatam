using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CentralPsi.Web.Options;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Services;

/// <summary>
/// Flow (flow.cl) payment integration - used while Transbank's own production API key is pending. With
/// Flow:Environment = "Sandbox" (the default) it talks to sandbox.flow.cl using self-service sandbox
/// credentials (no approval wait). Switch to "Production" and set ApiKey/SecretKey once Flow issues real
/// production ones. Requests are signed with HMAC-SHA256 per Flow's documented scheme: every parameter
/// (including apiKey, excluding the signature itself) is sorted by key, concatenated as key+value with no
/// separators, and HMAC-SHA256'd (hex) using SecretKey as the key; the resulting "s" param is then added to
/// the request on top of the signed parameters.
/// </summary>
public class FlowPaymentService : IPaymentService
{
    private readonly FlowOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FlowPaymentService> _logger;

    public FlowPaymentService(IOptions<FlowOptions> options, IHttpClientFactory httpClientFactory, ILogger<FlowPaymentService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private string BaseUrl => _options.Environment.Equals("Production", StringComparison.OrdinalIgnoreCase)
        ? "https://www.flow.cl/api"
        : "https://sandbox.flow.cl/api";

    private string Sign(SortedDictionary<string, string> parameters)
    {
        var toSign = string.Concat(parameters.Select(kv => kv.Key + kv.Value));
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.SecretKey ?? string.Empty), Encoding.UTF8.GetBytes(toSign));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<PaymentCreateResult> CreateTransactionAsync(string buyOrder, string sessionId, decimal amount, string returnUrl, string? confirmationUrl = null, string? payerEmail = null, CancellationToken ct = default)
    {
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["apiKey"] = _options.ApiKey ?? string.Empty,
            ["commerceOrder"] = buyOrder,
            ["subject"] = "Sesión psicológica CentralPsi",
            ["currency"] = "CLP",
            ["amount"] = ((long)amount).ToString(),
            ["email"] = string.IsNullOrWhiteSpace(payerEmail) ? "paciente@centralpsi.cl" : payerEmail,
            ["urlConfirmation"] = confirmationUrl ?? returnUrl,
            ["urlReturn"] = returnUrl,
        };
        var signature = Sign(parameters);

        var form = parameters.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)).ToList();
        form.Add(new KeyValuePair<string, string>("s", signature));

        var http = _httpClientFactory.CreateClient();
        var response = await http.PostAsync($"{BaseUrl}/payment/create", new FormUrlEncodedContent(form), ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Flow payment/create devolvió {Status}: {Body}", response.StatusCode, json);
            throw new InvalidOperationException($"Flow payment/create falló con {response.StatusCode}: {json}");
        }

        using var doc = JsonDocument.Parse(json);
        var url = doc.RootElement.GetProperty("url").GetString()!;
        var token = doc.RootElement.GetProperty("token").GetString()!;
        return new PaymentCreateResult(token, $"{url}?token={token}", RedirectMethod: "GET");
    }

    public async Task<PaymentCommitResult> CommitTransactionAsync(string token, CancellationToken ct = default)
    {
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["apiKey"] = _options.ApiKey ?? string.Empty,
            ["token"] = token,
        };
        var signature = Sign(parameters);

        var query = $"apiKey={Uri.EscapeDataString(parameters["apiKey"])}&token={Uri.EscapeDataString(token)}&s={Uri.EscapeDataString(signature)}";
        var http = _httpClientFactory.CreateClient();
        var response = await http.GetAsync($"{BaseUrl}/payment/getStatus?{query}", ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        // A purchase the payer anulled before ever entering payment data, or one Flow otherwise never fully
        // created, can make getStatus answer with a non-2xx or a non-JSON error body instead of a normal
        // status payload. Treat that the same as "not approved" instead of throwing - a failed lookup here
        // must never turn into a raw 500 for a paying patient.
        JsonDocument doc;
        try
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Flow payment/getStatus devolvió {Status} para el token {Token}: {Body}", response.StatusCode, token, json);
                return new PaymentCommitResult(false, "UNKNOWN", (int)response.StatusCode, null, null, null, json);
            }
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Flow payment/getStatus devolvió un cuerpo no-JSON para el token {Token}: {Body}", token, json);
            return new PaymentCommitResult(false, "UNKNOWN", null, null, null, null, json);
        }

        using (doc)
        {
            try
            {
                var root = doc.RootElement;
                var statusCode = ReadInt(root, "status");
                // Flow status codes: 1 pending, 2 paid, 3 rejected, 4 cancelled/nulled.
                var isApproved = statusCode == 2;
                var statusName = statusCode switch
                {
                    1 => "PENDING",
                    2 => "PAID",
                    3 => "REJECTED",
                    4 => "CANCELLED",
                    _ => "UNKNOWN"
                };

                decimal? paidAmount = null;
                DateTime? paidAtUtc = null;
                if (root.TryGetProperty("paymentData", out var paymentData))
                {
                    paidAmount = ReadDecimalOrNull(paymentData, "amount");
                    if (paymentData.TryGetProperty("date", out var dateEl) && dateEl.ValueKind == JsonValueKind.String
                        && DateTime.TryParse(dateEl.GetString(), out var parsedDate))
                    {
                        paidAtUtc = DateTime.SpecifyKind(parsedDate, DateTimeKind.Local).ToUniversalTime();
                    }
                }

                return new PaymentCommitResult(isApproved, statusName, statusCode, null, paidAtUtc, paidAmount, json);
            }
            catch (Exception ex)
            {
                // Flow's getStatus payload didn't match the expected shape (e.g. "status" as a string instead
                // of a number) - log the raw body so the actual shape can be fixed, but never let a paying
                // patient see a raw 500 over a JSON-shape mismatch.
                _logger.LogError(ex, "No se pudo interpretar la respuesta de Flow payment/getStatus para el token {Token}: {Body}", token, json);
                return new PaymentCommitResult(false, "UNKNOWN", null, null, null, null, json);
            }
        }
    }

    private static int ReadInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var el)) return 0;
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.GetInt32(),
            JsonValueKind.String => int.TryParse(el.GetString(), out var parsed) ? parsed : 0,
            _ => 0
        };
    }

    private static decimal? ReadDecimalOrNull(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetDecimal(out var num) => num,
            JsonValueKind.String when decimal.TryParse(el.GetString(), out var num) => num,
            _ => null
        };
    }
}
