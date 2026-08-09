using CentralPsi.Web.Options;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;

namespace CentralPsi.Web.Services;

/// <summary>
/// Automated first pass at verifying a professional's Ministerio de Salud certificate:
///  1) looks up the validation code on the Superintendencia de Salud's public verification page, and
///  2) if the uploaded file is an image, decodes its QR code and checks it references the same code/site.
///
/// The lookup page (confirmed against a live certificate on 2026-08-08) renders a banner
/// "Estado del certificado: VIGENTE" plus ID/NOMBRE ASOCIADO/RUN/fechas fields, but only after client-side
/// JavaScript runs (Angular) and a background reCAPTCHA v3 check clears - a plain HttpClient GET never sees
/// that text. Since this is the same public lookup tool anyone verifying a certificate normally uses (not a
/// private or rate-limited endpoint), a real headless browser (Playwright/Chromium) renders the page exactly
/// like a normal visitor's browser would, then we read the rendered text with the same keyword matching used
/// before. If the page's own backend ever decides to block on reCAPTCHA score, this degrades to "Inconclusive"
/// exactly like before, and the admin dashboard's manual approve/reject stays the real gate.
/// </summary>
public class SuperSaludCertificateValidationService : ICertificateValidationService
{
    private readonly SuperSaludOptions _options;
    private readonly ILogger<SuperSaludCertificateValidationService> _logger;

    private static readonly string[] ValidKeywords = { "estado del certificado: vigente", "certificado válido", "documento válido", "vigente" };
    private static readonly string[] InvalidKeywords = { "no vigente", "no existe", "no encontrado", "no se encontró", "no válido", "inválido", "sin resultados", "revocado", "anulado" };

    public SuperSaludCertificateValidationService(
        IOptions<SuperSaludOptions> options,
        ILogger<SuperSaludCertificateValidationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CertificateValidationResult> ValidateAsync(string validationCode, string certificateFilePathOnDisk, CancellationToken ct = default)
    {
        var notes = new List<string>();
        bool? webLookupValid = null;

        try
        {
            var renderedText = await RenderValidationPageTextAsync(validationCode, ct);
            var normalized = renderedText.ToLowerInvariant();
            var snippet = renderedText.Length > 400 ? renderedText[..400] : renderedText;

            if (InvalidKeywords.Any(k => normalized.Contains(k)))
            {
                webLookupValid = false;
                notes.Add("La página de validación de la Superintendencia de Salud indicó que el código no es válido.");
                _logger.LogInformation("[SuperSaludCheck] código {Code}: INVÁLIDO. Texto renderizado: {Snippet}", validationCode, snippet);
            }
            else if (ValidKeywords.Any(k => normalized.Contains(k)))
            {
                webLookupValid = true;
                notes.Add("La página de validación de la Superintendencia de Salud confirmó el código.");
                _logger.LogInformation("[SuperSaludCheck] código {Code}: VÁLIDO. Texto renderizado: {Snippet}", validationCode, snippet);
            }
            else
            {
                notes.Add("No se pudo interpretar automáticamente la respuesta de la Superintendencia de Salud; requiere revisión manual.");
                _logger.LogInformation("[SuperSaludCheck] código {Code}: INCONCLUSO. Texto renderizado ({Length} caracteres): {Snippet}", validationCode, renderedText.Length, snippet);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SuperSaludCheck] código {Code}: ERROR al renderizar la página", validationCode);
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

    private async Task<string> RenderValidationPageTextAsync(string validationCode, CancellationToken ct)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" }
        });
        var page = await browser.NewPageAsync();

        var url = $"{_options.ValidationBaseUrl}?id={Uri.EscapeDataString(validationCode)}";
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });

        // The ?id= query param only pre-loads the form - a real visitor still has to type the code and press
        // "Consultar Certificado" to actually trigger the (reCAPTCHA-gated) lookup, so do the same here:
        // explicitly fill the ID field (don't rely on the query param having done it) and click the button.
        var idInput = page.Locator("input").First;
        if (await idInput.IsVisibleAsync())
        {
            await idInput.FillAsync(validationCode);
        }

        var searchButton = page.Locator("button:has-text('Consultar Certificado')");
        if (await searchButton.CountAsync() > 0)
        {
            await searchButton.First.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 20000 });
        }

        // Give the Angular app + background reCAPTCHA check a moment to finish rendering the result banner.
        await page.WaitForTimeoutAsync(2000);

        return await page.InnerTextAsync("body");
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
