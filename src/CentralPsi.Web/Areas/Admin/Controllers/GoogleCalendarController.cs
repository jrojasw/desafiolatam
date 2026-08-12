using System.Text.Json;
using CentralPsi.Web.Data.Seed;
using CentralPsi.Web.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Areas.Admin.Controllers;

/// <summary>
/// One-time OAuth setup so Google Calendar/Meet event creation works with a free Google account (no Workspace
/// domain-wide delegation required). The admin clicks "Conectar", grants access, and Google redirects back here
/// with an authorization code that gets exchanged for a refresh token - which then has to be copied into the
/// GoogleCalendar:RefreshToken app setting (an env var in production) by hand, since this app has no writable
/// persistent config store to save it into automatically.
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
    private readonly ILogger<GoogleCalendarController> _logger;

    public GoogleCalendarController(
        IOptions<GoogleCalendarOptions> options,
        IOptions<AppOptions> appOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleCalendarController> logger)
    {
        _options = options.Value;
        _appOptions = appOptions.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private string RedirectUri => $"{_appOptions.BaseUrl}/Admin/GoogleCalendar/Callback";

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.HasClientCredentials = !string.IsNullOrWhiteSpace(_options.ClientId) && !string.IsNullOrWhiteSpace(_options.ClientSecret);
        ViewBag.HasRefreshToken = !string.IsNullOrWhiteSpace(_options.RefreshToken);
        ViewBag.Enabled = _options.Enabled;
        ViewBag.RedirectUri = RedirectUri;
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

        ViewBag.RefreshToken = refreshTokenElement.GetString();
        return View("Callback");
    }
}
