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
///    GoogleCalendarOptions.ClientId/ClientSecret/RefreshToken. No Google Workspace required.
///  - Service account with Workspace domain-wide delegation (GoogleCalendarOptions.ImpersonateUser), for anyone
///    who already has Workspace and a service account JSON key.
/// Until either is configured (and Enabled = true), this service logs and returns no Meet link so booking still
/// completes - the notification email tells the patient/professional the link will follow.
/// </summary>
public class GoogleCalendarService : IGoogleCalendarService
{
    private readonly GoogleCalendarOptions _options;
    private readonly ITimeZoneService _timeZoneService;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(IOptions<GoogleCalendarOptions> options, ITimeZoneService timeZoneService, ILogger<GoogleCalendarService> logger)
    {
        _options = options.Value;
        _timeZoneService = timeZoneService;
        _logger = logger;
    }

    public async Task<MeetEventResult> CreateSessionEventAsync(Appointment appointment, Professional professional, CancellationToken ct = default)
    {
        if (!IsConfigured())
        {
            _logger.LogWarning("Google Calendar no está configurado (GoogleCalendarOptions.Enabled=false); no se generará enlace de Meet para la cita {AppointmentId}.", appointment.Id);
            return new MeetEventResult(null, null);
        }

        try
        {
            var service = BuildCalendarService();

            var newEvent = new Event
            {
                Summary = $"Sesión CentralPsi: {professional.FullName} / {appointment.PatientFullName}",
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
            _logger.LogError(ex, "Error creando el evento de Google Calendar/Meet para la cita {AppointmentId}", appointment.Id);
            return new MeetEventResult(null, null);
        }
    }

    public async Task CancelSessionEventAsync(string googleEventId, CancellationToken ct = default)
    {
        if (!IsConfigured())
        {
            return;
        }

        try
        {
            var service = BuildCalendarService();
            await service.Events.Delete(_options.CalendarId, googleEventId).ExecuteAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelando el evento de Google Calendar {EventId}", googleEventId);
        }
    }

    private bool HasOAuthCredentials() =>
        !string.IsNullOrWhiteSpace(_options.ClientId)
        && !string.IsNullOrWhiteSpace(_options.ClientSecret)
        && !string.IsNullOrWhiteSpace(_options.RefreshToken);

    private bool IsConfigured() =>
        _options.Enabled
        && (HasOAuthCredentials() || !string.IsNullOrWhiteSpace(_options.ServiceAccountJson) || !string.IsNullOrWhiteSpace(_options.ServiceAccountJsonPath));

    private CalendarService BuildCalendarService()
    {
        IConfigurableHttpClientInitializer credential;
        if (HasOAuthCredentials())
        {
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets { ClientId = _options.ClientId, ClientSecret = _options.ClientSecret },
                Scopes = new[] { CalendarService.Scope.Calendar }
            });
            credential = new UserCredential(flow, "centralpsi-agenda", new TokenResponse { RefreshToken = _options.RefreshToken });
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
