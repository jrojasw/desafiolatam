using System.Text.Json;
using CentralPsi.Web.Data.Seed;
using CentralPsi.Web.Options;
using CentralPsi.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Areas.Admin.Controllers;

/// <summary>
/// One-time OAuth setup so Google Calendar/Meet event creation works with a free Google account (no Workspace
/// domain-wide delegation required). The admin clicks "Conectar", grants access, and Google redirects back here
/// with an authorization code that gets exchanged for a refresh token - which Callback now saves straight to the
/// database (encrypted, via IIntegrationSettingsService) instead of just displaying it for a human to copy into
/// Render by hand. GoogleCalendar:RefreshToken (the env var) still works as a fallback for the very first deploy,
/// but every reconnect after that is self-contained: no copy-paste step, nothing to get wrong.
/// </summary>
[Area("Admin")]
[Authorize(Roles = DataSeeder.AdminRole)]
[Route("Admin/GoogleCalendar")]
public class GoogleCalendarController : Controller
{
    private const string Scope = "https://www.googleapis.com/auth/calendar";

    private readonly GoogleCalendarOptions _options;
    private readonly AppOptions _appOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGoogleCalendarService _calendarService;
    private readonly IIntegrationSettingsService _settings;
    private readonly ILogger<GoogleCalendarController> _logger;

    public GoogleCalendarController(
        IOptions<GoogleCalendarOptions> options,
        IOptions<AppOptions> appOptions,
        IHttpClientFactory httpClientFactory,
        IGoogleCalendarService calendarService,
        IIntegrationSettingsService settings,
        ILogger<GoogleCalendarController> logger)
    {
        _options = options.Value;
        _appOptions = appOptions.Value;
        _httpClientFactory = httpClientFactory;
        _calendarService = calendarService;
        _settings = settings;
        _logger = logger;
    }

    private string RedirectUri => $"{_appOptions.BaseUrl}/Admin/GoogleCalendar/Callback";

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var status = await _calendarService.GetStatusAsync();
        var history = await _calendarService.GetEventHistoryAsync();
        ViewBag.HasClientCredentials = status.HasClientCredentials;
        ViewBag.HasRefreshToken = status.Configured;
        ViewBag.TokenSource = status.TokenSource;
        ViewBag.TokenUpdatedAtUtc = status.TokenUpdatedAtUtc;
        ViewBag.Enabled = _options.Enabled;
        ViewBag.RedirectUri = RedirectUri;
        ViewBag.LastEventSuccessAtUtc = history.LastSuccessAtUtc;
        ViewBag.LastEventErrorAtUtc = history.LastErrorAtUtc;
        ViewBag.LastEventErrorMessage = history.LastErrorMessage;
        return View();
    }

    [HttpGet("Connect")]
    public IActionResult Connect()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            TempData["ErrorMessage"] = "Falta configurar GoogleCalendar:ClientId / ClientSecret antes de poder conectar.";
            return RedirectToAction(nameof(Index));
        }

        var authUrl = "https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString(Scope)}" +
            "&access_type=offline" +
            "&prompt=consent";
        return Redirect(authUrl);
    }

    [HttpGet("Callback")]
    public async Task<IActionResult> Callback(string? code, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            TempData["ErrorMessage"] = $"Google rechazó la autorización: {error}";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["ErrorMessage"] = "No se recibió un código de autorización de Google.";
            return RedirectToAction(nameof(Index));
        }

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId!,
            ["client_secret"] = _options.ClientSecret!,
            ["redirect_uri"] = RedirectUri,
            ["grant_type"] = "authorization_code"
        }));

        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error intercambiando el código OAuth de Google Calendar: {Body}", body);
            TempData["ErrorMessage"] = "Google rechazó el intercambio del código. Revisa los logs para más detalle.";
            return RedirectToAction(nameof(Index));
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement))
        {
            TempData["ErrorMessage"] = "Google no devolvió un refresh_token. Vuelve a intentarlo (a veces solo lo entrega la primera vez que autorizas la app).";
            return RedirectToAction(nameof(Index));
        }

        var refreshToken = refreshTokenElement.GetString();
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            TempData["ErrorMessage"] = "Google devolvió un refresh_token vacío.";
            return RedirectToAction(nameof(Index));
        }

        await _settings.SetAsync(IntegrationSettingKeys.GoogleCalendarRefreshToken, refreshToken.Trim());
        _logger.LogInformation("Nuevo refresh token de Google Calendar guardado en la base de datos.");

        ViewBag.ConnectedEmail = await _calendarService.GetConnectedAccountEmailAsync();
        return View("Callback");
    }

    /// <summary>Creates and immediately deletes a short-lived test event (with a Meet link request) through the
    /// exact same credentials CreateSessionEventAsync uses for real bookings, so the admin can verify the
    /// connection right away instead of waiting for a real appointment to find out it's broken.</summary>
    [HttpPost("TestConnection")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection()
    {
        var result = await _calendarService.TestConnectionAsync();

        var diagnostics =
            $"Origen del token: {result.TokenSource}. " +
            (result.TokenLength is int len ? $"Largo: {len} caracteres. " : "Sin token. ") +
            (result.TokenStartsWithExpectedPrefix is bool startsOk ? $"Empieza con \"1//\": {(startsOk ? "sí" : "no")}. " : "") +
            (result.TokenHadWhitespace is bool hadWs ? $"Tenía espacios/saltos de línea: {(hadWs ? "sí" : "no")}. " : "") +
            (result.ClientIdFirst12 is not null ? $"Client ID empieza con \"{result.ClientIdFirst12}...\". " : "") +
            (result.TokenUpdatedAtUtc is DateTime updated ? $"Guardado el {updated:yyyy-MM-dd HH:mm} UTC." : "Sin fecha de guardado (viene de variable de entorno, no de la base de datos).");

        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Success
            ? $"Conexión OK: se creó y eliminó un evento de prueba con enlace de Meet. Conectado como {result.ConnectedEmail ?? "(correo no disponible)"}. {diagnostics}"
            : $"Error de conexión ({result.ErrorType}): {result.ErrorMessage} — {diagnostics}";

        return RedirectToAction(nameof(Index));
    }
}
