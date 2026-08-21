namespace CentralPsi.Web.Services;

/// <summary>RedirectMethod is "POST" for Transbank (the browser posts token_ws to RedirectUrl) or "GET" for
/// Flow (RedirectUrl already carries ?token=... and the browser just navigates there).</summary>
public record PaymentCreateResult(string Token, string RedirectUrl, string RedirectMethod = "POST");

public record PaymentCommitResult(
    bool IsApproved,
    string Status,
    int? ResponseCode,
    string? AuthorizationCode,
    DateTime? TransactionDateUtc,
    decimal? Amount,
    string RawJson);

public interface IPaymentService
{
    /// <summary>confirmationUrl and payerEmail are only used by providers that need them (Flow requires both
    /// a server-to-server confirmation webhook and a payer email; Transbank ignores them).</summary>
    Task<PaymentCreateResult> CreateTransactionAsync(string buyOrder, string sessionId, decimal amount, string returnUrl, string? confirmationUrl = null, string? payerEmail = null, CancellationToken ct = default);

    /// <summary>Confirms the transaction after the provider redirects back (or calls the confirmation
    /// webhook). Refunds are handled manually per CentralPsi's policy (see IRefundCalculationService /
    /// reembolsos@centralpsi.cl), not through this API.</summary>
    Task<PaymentCommitResult> CommitTransactionAsync(string token, CancellationToken ct = default);
}
