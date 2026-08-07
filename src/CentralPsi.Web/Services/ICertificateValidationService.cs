namespace CentralPsi.Web.Services;

public record CertificateValidationResult(bool IsValid, bool Inconclusive, string Notes, string? QrRawData);

public interface ICertificateValidationService
{
    /// <summary>
    /// Cross-checks the validation code against the Superintendencia de Salud lookup page and, when the
    /// uploaded file is an image, decodes its embedded QR code as a second signal.
    /// </summary>
    Task<CertificateValidationResult> ValidateAsync(string validationCode, string certificateFilePathOnDisk, CancellationToken ct = default);
}
