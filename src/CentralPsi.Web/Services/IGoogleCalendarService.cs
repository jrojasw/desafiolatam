using CentralPsi.Web.Models.Entities;

namespace CentralPsi.Web.Services;

public record MeetEventResult(string? EventId, string? MeetLink);

public interface IGoogleCalendarService
{
    Task<MeetEventResult> CreateSessionEventAsync(Appointment appointment, Professional professional, CancellationToken ct = default);
    Task CancelSessionEventAsync(string googleEventId, CancellationToken ct = default);
}
