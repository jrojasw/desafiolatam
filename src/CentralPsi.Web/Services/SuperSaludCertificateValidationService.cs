using CentralPsi.Web.Options;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;

namespace CentralPsi.Web.Services;

/// <summary>
/// Automated first pass at verifying a professional's Ministerio de Salud certificate:
///  1) looks up the validation code on the Superintendencia de Salud's public verification page, and
///  2) if the uploaded file is an image, decodes its QR code and checks it references the same code/site.
///
/// NOTE: this environment's outbound network policy blocks emisorcertificados.superdesalud.gob.cl, so the
/// exact HTML markers below (ValidKeywords / InvalidKeywords) could not be verified against a live response
/// while building this. They should be confirmed/tuned against a real certificate before relying on the
/// automatic pass in production - until then, treat "Inconclusive" results as the safe default and keep the
/// admin dashboard's manual approve/reject as the actual gate for publishing a professional.
/// </summary>
public class SuperSaludCertificateValidationService : ICertificateValidationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SuperSaludOptions _options;
    private readonly ILogger<SuperSaludCertificateValidationService> _logger;

    private static readonly string[] ValidKeywords = { "certificado válido", "certificado vigente", "documento válido", "válido" };
    private static readonly string[] InvalidKeywords = { "no existe", "no encontrado", "no válido", "inválido", "sin resultados" };

    public SuperSaludCertificateValidationService(
        IHttpClientFactory httpClientFactory,
        IOptions<SuperSaludOptions> options,
        ILogger<SuperSaludCertificateValidationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CertificateValidationResult> ValidateAsync(string validationCode, string certificateFilePathOnDisk, CancellationToken ct = default)
    {
        var notes = new List<string>();
        bool? webLookupValid = null;

        try
        {
            var client = _httpClientFactory.CreateClient("SuperSalud");
            var url = $"{_options.ValidationBaseUrl}?id={Uri.EscapeDataString(validationCode)}";
            var response = await client.GetAsync(url, ct);
            var html = await response.Content.ReadAsStringAsync(ct);
            var normalized = html.ToLowerInvariant();

            if (InvalidKeywords.Any(k => normalized.Contains(k)))
            {
                webLookupValid = false;
                notes.Add("La página de validación de la Superintendencia de Salud indicó que el código no es válido.");
            }
            else if (ValidKeywords.Any(k => normalized.Contains(k)))
            {
                webLookupValid = true;
                notes.Add("La página de validación de la Superintendencia de Salud confirmó el código.");
            }
            else
            {
                notes.Add("No se pudo interpretar automáticamente la respuesta de la Superintendencia de Salud; requiere revisión manual.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error consultando el sitio de validación de la Superintendencia de Salud");
            notes.Add($"No se pudo contactar el sitio de validación automáticamente ({ex.Message}); requiere revisión manual.");
        }

        string? qrRawData = null;
        bool? qrMatches = null;
        var extension = Path.GetExtension(certificateFilePathOnDisk).ToLowerInvariant();
        if (extension is ".jpg" or ".jpeg" or ".png" or ".webp")
        {
            try
            {
                qrRawData = DecodeQrCode(certificateFilePathOnDisk);
                if (qrRawData is not null)
                {
                    qrMatches = qrRawData.Contains(validationCode, StringComparison.OrdinalIgnoreCase);
                    notes.Add(qrMatches == true
                        ? "El código QR del certificado coincide con el código de validación ingresado."
                        : "El código QR del certificado fue leído pero no coincide con el código ingresado.");
                }
                else
                {
                    notes.Add("No se detectó un código QR legible en el archivo del certificado.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error leyendo el código QR del certificado");
                notes.Add($"No se pudo leer el código QR del certificado ({ex.Message}).");
            }
        }
        else
        {
            notes.Add("El certificado fue subido en PDF; la lectura automática de QR solo aplica a imágenes, revisar manualmente.");
        }

        var isValid = webLookupValid == true && qrMatches != false;
        var inconclusive = webLookupValid is null || qrRawData is null;

        return new CertificateValidationResult(isValid, inconclusive, string.Join(" ", notes), qrRawData);
    }

    private static string? DecodeQrCode(string filePath)
    {
        // ZXing.Net.Bindings.ImageSharp only ever shipped against SixLabors.ImageSharp 1.0.4's pixel-access
        // API, which no longer exists in the 3.x line used here (free license, no v4+ paywall) - it throws
        // MissingMethodException at runtime. Reading the raw RGB24 bytes ourselves and feeding ZXing.Net's own
        // RGBLuminanceSource sidesteps that broken binding entirely.
        using var image = Image.Load<Rgb24>(filePath);
        var pixelBytes = new byte[image.Width * image.Height * 3];
        image.CopyPixelDataTo(pixelBytes);

        var luminanceSource = new RGBLuminanceSource(pixelBytes, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.RGB24);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }
            }
        };
        var result = reader.Decode(luminanceSource);
        return result?.Text;
    }
}
