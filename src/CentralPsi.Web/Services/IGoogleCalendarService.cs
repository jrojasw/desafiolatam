using CentralPsi.Web.Models.Entities;

namespace CentralPsi.Web.Services;

public record MeetEventResult(string? EventId, string? MeetLink);

/// <summary>Result of a connection self-test (Admin/GoogleCalendar's "Probar conexión" button). The diagnostic
/// fields (TokenSource..ClientIdLast6) never carry a secret, only enough to tell operators whether the
/// right credentials are in play - safe to render directly to an admin.</summary>
public record CalendarConnectionTestResult(
    bool Success,
    string? MeetLink,
    string? ErrorType,
    string? ErrorMessage,
    string TokenSource,
    int? TokenLength,
    bool? TokenStartsWithExpectedPrefix,
    bool? TokenHadWhitespace,
    string? ClientIdFirst12,
    DateTime? TokenUpdatedAtUtc,
    string? ConnectedEmail);

/// <summary>Non-network status snapshot, used by the Admin/GoogleCalendar index page - HasRefreshToken/TokenSource
/// reflect whichever source (database or GoogleCalendar:RefreshToken env var) actually resolves, not just
/// whether the env var happens to be set.</summary>
public record GoogleCalendarStatus(bool Configured, bool HasClientCredentials, string TokenSource, DateTime? TokenUpdatedAtUtc);

/// <summary>Last known outcome of CreateSessionEventAsync, for the Admin/GoogleCalendar status panel.</summary>
public record GoogleCalendarEventHistory(DateTime? LastSuccessAtUtc, DateTime? LastErrorAtUtc, string? LastErrorMessage);

public interface IGoogleCalendarService
{
    Task<MeetEventResult> CreateSessionEventAsync(Appointment appointment, Professional professional, CancellationToken ct = default);
    Task CancelSessionEventAsync(string googleEventId, CancellationToken ct = default);

    /// <summary>Creates a short-lived test event (with a Meet link request) on the configured calendar, then
    /// deletes it immediately - exercises the exact same credentials/auth path as a real booking, without
    /// leaving anything behind, so the admin can verify the connection without waiting for a real appointment.</summary>
    Task<CalendarConnectionTestResult> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>Looks up the connected account's own email (the "primary" calendar's id) so the OAuth callback
    /// can show "Conectado como X" instead of displaying the refresh token.</summary>
    Task<string?> GetConnectedAccountEmailAsync(CancellationToken ct = default);

    Task<GoogleCalendarStatus> GetStatusAsync(CancellationToken ct = default);

    Task<GoogleCalendarEventHistory> GetEventHistoryAsync(CancellationToken ct = default);
}
