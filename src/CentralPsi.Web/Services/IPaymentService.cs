namespace CentralPsi.Web.Services;

public record PaymentCreateResult(string Token, string RedirectUrl);

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
    Task<PaymentCreateResult> CreateTransactionAsync(string buyOrder, string sessionId, decimal amount, string returnUrl, CancellationToken ct = default);

    /// <summary>Confirms the transaction after Transbank redirects back. Refunds are handled manually per
    /// CentralPsi's policy (see IRefundCalculationService / reembolsos@centralpsi.cl), not through this API.</summary>
    Task<PaymentCommitResult> CommitTransactionAsync(string token, CancellationToken ct = default);
}
