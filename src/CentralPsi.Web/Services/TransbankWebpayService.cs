using System.Text.Json;
using CentralPsi.Web.Options;
using Microsoft.Extensions.Options;
using Transbank.Common;
using Transbank.Webpay.WebpayPlus;

namespace CentralPsi.Web.Services;

/// <summary>
/// Webpay Plus integration. With Transbank:Environment = "Integration" (the default) it runs against
/// Transbank's public sandbox using their published test commerce code/API key - no credentials needed to try
/// it end to end. Switch to "Production" and set CommerceCode/ApiKey once Transbank issues real ones.
/// </summary>
public class TransbankWebpayService : IPaymentService
{
    private readonly Transaction _transaction;

    public TransbankWebpayService(IOptions<TransbankOptions> options)
    {
        var opt = options.Value;
        _transaction = opt.Environment.Equals("Production", StringComparison.OrdinalIgnoreCase)
            ? Transaction.buildForProduction(opt.CommerceCode!, opt.ApiKey!)
            : Transaction.buildForIntegration(IntegrationCommerceCodes.WEBPAY_PLUS, IntegrationApiKeys.WEBPAY);
    }

    public Task<PaymentCreateResult> CreateTransactionAsync(string buyOrder, string sessionId, decimal amount, string returnUrl, string? confirmationUrl = null, string? payerEmail = null, CancellationToken ct = default)
    {
        var response = _transaction.Create(buyOrder, sessionId, amount, returnUrl);
        return Task.FromResult(new PaymentCreateResult(response.Token, response.Url));
    }

    public Task<PaymentCommitResult> CommitTransactionAsync(string token, CancellationToken ct = default)
    {
        var response = _transaction.Commit(token);
        var isApproved = string.Equals(response.Status, "AUTHORIZED", StringComparison.OrdinalIgnoreCase)
            && response.ResponseCode == 0;

        var result = new PaymentCommitResult(
            isApproved,
            response.Status ?? "UNKNOWN",
            response.ResponseCode,
            response.AuthorizationCode,
            response.TransactionDate,
            response.Amount is null ? null : Convert.ToDecimal(response.Amount),
            JsonSerializer.Serialize(response));
        return Task.FromResult(result);
    }
}
