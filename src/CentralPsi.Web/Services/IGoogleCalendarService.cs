using CentralPsi.Web.Models.Entities;

namespace CentralPsi.Web.Services;

public record MeetEventResult(string? EventId, string? MeetLink);

/// <summary>Result of a connection self-test (Admin/GoogleCalendar's "Probar conexión" button). ErrorType/
/// ErrorMessage carry only the exception's type name and message (e.g. "invalid_grant") - never a token or any
/// other secret, since this is meant to be shown directly to the admin.</summary>
public record CalendarConnectionTestResult(bool Success, string? MeetLink, string? ErrorType, string? ErrorMessage);

public interface IGoogleCalendarService
{
    Task<MeetEventResult> CreateSessionEventAsync(Appointment appointment, Professional professional, CancellationToken ct = default);
    Task CancelSessionEventAsync(string googleEventId, CancellationToken ct = default);

    /// <summary>Creates a short-lived test event (with a Meet link request) on the configured calendar, then
    /// deletes it immediately - exercises the exact same credentials/auth path as a real booking, without
    /// leaving anything behind, so the admin can verify the connection without waiting for a real appointment.</summary>
    Task<CalendarConnectionTestResult> TestConnectionAsync(CancellationToken ct = default);
}
