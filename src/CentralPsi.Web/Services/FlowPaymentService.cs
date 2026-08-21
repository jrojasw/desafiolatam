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
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Flow payment/getStatus devolvió {Status}: {Body}", response.StatusCode, json);
            throw new InvalidOperationException($"Flow payment/getStatus falló con {response.StatusCode}: {json}");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var statusCode = root.TryGetProperty("status", out var statusEl) ? statusEl.GetInt32() : 0;
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
            if (paymentData.TryGetProperty("amount", out var amountEl) && amountEl.TryGetDecimal(out var amt))
            {
                paidAmount = amt;
            }
            if (paymentData.TryGetProperty("date", out var dateEl) && DateTime.TryParse(dateEl.GetString(), out var parsedDate))
            {
                paidAtUtc = DateTime.SpecifyKind(parsedDate, DateTimeKind.Local).ToUniversalTime();
            }
        }

        return new PaymentCommitResult(isApproved, statusName, statusCode, null, paidAtUtc, paidAmount, json);
    }
}
