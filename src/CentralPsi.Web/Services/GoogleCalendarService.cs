using CentralPsi.Web.Models.Entities;
using CentralPsi.Web.Options;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Services;

/// <summary>
/// Creates the Google Meet-backed calendar event for a confirmed appointment. Supports two auth paths:
///  - OAuth (preferred): a free Google account authorized once via /Admin/GoogleCalendar/Connect, using
///    GoogleCalendarOptions.ClientId/ClientSecret and a refresh token. No Google Workspace required.
///  - Service account with Workspace domain-wide delegation (GoogleCalendarOptions.ImpersonateUser), for anyone
///    who already has Workspace and a service account JSON key.
/// Until either is configured (and Enabled = true), this service logs and returns no Meet link so booking still
/// completes - the notification email tells the patient/professional the link will follow.
///
/// The OAuth refresh token itself is resolved from IIntegrationSettingsService (encrypted in the database, set
/// automatically by GoogleCalendarController.Callback the moment a connection succeeds) first, falling back to
/// the GoogleCalendar:RefreshToken app setting/env var only if nothing is stored yet - that removes the manual
/// "copy the token, paste it into Render" step (and its associated copy-paste errors) from every reconnect after
/// the first one. BuildCalendarService() builds a fresh credential/client on every call; nothing is cached, so
/// there's no stale-credential cache to invalidate when a new token is saved.
/// </summary>
public class GoogleCalendarService : IGoogleCalendarService
{
    private readonly GoogleCalendarOptions _options;
    private readonly IIntegrationSettingsService _settings;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(IOptions<GoogleCalendarOptions> options, IIntegrationSettingsService settings, ILogger<GoogleCalendarService> logger)
    {
        _options = options.Value;
        _settings = settings;
        _logger = logger;
    }

    private record ResolvedCredentials(bool Configured, string? RefreshToken, string TokenSource, DateTime? TokenUpdatedAtUtc, bool TokenHadWhitespace);

    private async Task<ResolvedCredentials> ResolveAsync(CancellationToken ct)
    {
        var clientId = _options.ClientId?.Trim();
        var clientSecret = _options.ClientSecret?.Trim();
        var hasOAuthClient = !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
        var hasServiceAccount = !string.IsNullOrWhiteSpace(_options.ServiceAccountJson) || !string.IsNullOrWhiteSpace(_options.ServiceAccountJsonPath);

        var fromDb = await _settings.GetAsync(IntegrationSettingKeys.GoogleCalendarRefreshToken, ct);
        if (!string.IsNullOrWhiteSpace(fromDb))
        {
            var updatedAt = await _settings.GetUpdatedAtUtcAsync(IntegrationSettingKeys.GoogleCalendarRefreshToken, ct);
            return new ResolvedCredentials(_options.Enabled && hasOAuthClient, fromDb.Trim(), "Base de datos", updatedAt, fromDb != fromDb.Trim());
        }

        var fromEnv = _options.RefreshToken;
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return new ResolvedCredentials(_options.Enabled && hasOAuthClient, fromEnv.Trim(), "Variable de entorno", null, fromEnv != fromEnv.Trim());
        }

        return new ResolvedCredentials(_options.Enabled && hasServiceAccount, null, "Ninguno (sin OAuth; usa cuenta de servicio si está configurada)", null, false);
    }

    public async Task<GoogleCalendarStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(ct);
        var hasClientCredentials = !string.IsNullOrWhiteSpace(_options.ClientId) && !string.IsNullOrWhiteSpace(_options.ClientSecret);
        return new GoogleCalendarStatus(resolved.Configured, hasClientCredentials, resolved.TokenSource, resolved.TokenUpdatedAtUtc);
    }

    public async Task<MeetEventResult> CreateSessionEventAsync(Appointment appointment, Professional professional, CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(ct);
        if (!resolved.Configured)
        {
            _logger.LogWarning("Google Calendar no está configurado (fuente del token: {Source}); no se generará enlace de Meet para la cita {AppointmentId}.", resolved.TokenSource, appointment.Id);
            return new MeetEventResult(null, null);
        }

        try
        {
            var service = BuildCalendarService(resolved.RefreshToken);

            var attendeeLabel = appointment.IsForMinor
                ? $"{appointment.MinorFullName} ({appointment.MinorAge} años, a cargo de {appointment.PatientFullName})"
                : appointment.PatientFullName;

            var newEvent = new Event
            {
                Summary = $"Sesión CentralPsi: {professional.FullName} / {attendeeLabel}",
                Description = "Sesión agendada a través de CentralPsi. Este evento fue generado automáticamente.",
                Start = new EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(appointment.ScheduledStartUtc, TimeSpan.Zero) },
                End = new EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(appointment.ScheduledEndUtc, TimeSpan.Zero) },
                Attendees = new List<EventAttendee>
                {
                    new() { Email = appointment.PatientEmail, DisplayName = appointment.PatientFullName },
                    new() { Email = professional.Email, DisplayName = professional.FullName }
                },
                ConferenceData = new ConferenceData
                {
                    CreateRequest = new CreateConferenceRequest
                    {
                        RequestId = appointment.Id.ToString("N"),
                        ConferenceSolutionKey = new ConferenceSolutionKey { Type = "hangoutsMeet" }
                    }
                },
                GuestsCanModify = false,
            };

            var request = service.Events.Insert(newEvent, _options.CalendarId);
            request.ConferenceDataVersion = 1;
            request.SendUpdates = EventsResource.InsertRequest.SendUpdatesEnum.All;

            var created = await request.ExecuteAsync(ct);
            var meetLink = created.HangoutLink
                ?? created.ConferenceData?.EntryPoints?.FirstOrDefault(e => e.EntryPointType == "video")?.Uri;

            return new MeetEventResult(created.Id, meetLink);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando el evento de Google Calendar/Meet para la cita {AppointmentId} (fuente del token: {Source})", appointment.Id, resolved.TokenSource);
            return new MeetEventResult(null, null);
        }
    }

    public async Task CancelSessionEventAsync(string googleEventId, CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(ct);
        if (!resolved.Configured)
        {
            return;
        }

        try
        {
            var service = BuildCalendarService(resolved.RefreshToken);
            await service.Events.Delete(_options.CalendarId, googleEventId).ExecuteAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelando el evento de Google Calendar {EventId}", googleEventId);
        }
    }

    public async Task<string?> GetConnectedAccountEmailAsync(CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(ct);
        if (!resolved.Configured)
        {
            return null;
        }

        try
        {
            var service = BuildCalendarService(resolved.RefreshToken);
            var primary = await service.CalendarList.Get("primary").ExecuteAsync(ct);
            return primary.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo determinar la cuenta de Google Calendar conectada");
            return null;
        }
    }

    public async Task<CalendarConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(ct);

        var clientId = _options.ClientId?.Trim();
        var clientIdLast6 = string.IsNullOrEmpty(clientId) ? null : clientId[Math.Max(0, clientId.Length - 6)..];
        var tokenLength = resolved.RefreshToken?.Length;
        var startsWithPrefix = resolved.RefreshToken is null ? (bool?)null : resolved.RefreshToken.StartsWith("1//", StringComparison.Ordinal);

        if (!resolved.Configured)
        {
            return new CalendarConnectionTestResult(false, null, "NotConfigured",
                "Google Calendar no está configurado (GoogleCalendar:Enabled=false, falta Client ID/Secret, o no hay refresh token en la base de datos ni en el entorno).",
                resolved.TokenSource, tokenLength, startsWithPrefix, resolved.TokenHadWhitespace, clientIdLast6, resolved.TokenUpdatedAtUtc);
        }

        CalendarService service;
        try
        {
            service = BuildCalendarService(resolved.RefreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error construyendo el cliente de Google Calendar durante la prueba de conexión");
            return new CalendarConnectionTestResult(false, null, ex.GetType().Name, ex.Message,
                resolved.TokenSource, tokenLength, startsWithPrefix, resolved.TokenHadWhitespace, clientIdLast6, resolved.TokenUpdatedAtUtc);
        }

        Event? created = null;
        try
        {
            var testEvent = new Event
            {
                Summary = "Prueba de conexión CentralPsi",
                Description = "Evento de prueba generado desde el panel de administración para verificar la conexión con Google Calendar. Se elimina automáticamente.",
                Start = new EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.UtcNow.AddHours(1) },
                End = new EventDateTime { DateTimeDateTimeOffset = DateTimeOffset.UtcNow.AddHours(1).AddMinutes(15) },
                ConferenceData = new ConferenceData
                {
                    CreateRequest = new CreateConferenceRequest
                    {
                        RequestId = Guid.NewGuid().ToString("N"),
                        ConferenceSolutionKey = new ConferenceSolutionKey { Type = "hangoutsMeet" }
                    }
                }
            };

            var insertRequest = service.Events.Insert(testEvent, _options.CalendarId);
            insertRequest.ConferenceDataVersion = 1;
            created = await insertRequest.ExecuteAsync(ct);

            var meetLink = created.HangoutLink
                ?? created.ConferenceData?.EntryPoints?.FirstOrDefault(e => e.EntryPointType == "video")?.Uri;

            _logger.LogInformation("Prueba de conexión a Google Calendar exitosa (evento de prueba {EventId} creado y será eliminado, fuente del token: {Source})", created.Id, resolved.TokenSource);
            return new CalendarConnectionTestResult(true, meetLink, null, null,
                resolved.TokenSource, tokenLength, startsWithPrefix, resolved.TokenHadWhitespace, clientIdLast6, resolved.TokenUpdatedAtUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en la prueba de conexión a Google Calendar (fuente del token: {Source})", resolved.TokenSource);
            return new CalendarConnectionTestResult(false, null, ex.GetType().Name, ex.Message,
                resolved.TokenSource, tokenLength, startsWithPrefix, resolved.TokenHadWhitespace, clientIdLast6, resolved.TokenUpdatedAtUtc);
        }
        finally
        {
            if (created?.Id is not null)
            {
                try
                {
                    await service.Events.Delete(_options.CalendarId, created.Id).ExecuteAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo eliminar el evento de prueba {EventId} de Google Calendar", created.Id);
                }
            }
        }
    }

    private CalendarService BuildCalendarService(string? refreshToken)
    {
        IConfigurableHttpClientInitializer credential;
        var clientId = _options.ClientId?.Trim();
        var clientSecret = _options.ClientSecret?.Trim();
        var hasOAuthCredentials = !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret) && !string.IsNullOrWhiteSpace(refreshToken);

        if (hasOAuthCredentials)
        {
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                Scopes = new[] { CalendarService.Scope.Calendar }
            });
            credential = new UserCredential(flow, "centralpsi-agenda", new TokenResponse { RefreshToken = refreshToken!.Trim() });
        }
        else
        {
            var googleCredential = (!string.IsNullOrWhiteSpace(_options.ServiceAccountJson)
                    ? GoogleCredential.FromJson(_options.ServiceAccountJson)
                    : GoogleCredential.FromFile(_options.ServiceAccountJsonPath))
                .CreateScoped(CalendarService.Scope.Calendar);

            if (!string.IsNullOrWhiteSpace(_options.ImpersonateUser))
            {
                googleCredential = googleCredential.CreateWithUser(_options.ImpersonateUser);
            }
            credential = googleCredential;
        }

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "CentralPsi"
        });
    }
}
