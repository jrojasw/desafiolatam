using CentralPsi.Web.Models.Entities;

namespace CentralPsi.Web.Services;

public record AvailableSlot(DateTime StartUtc, DateTime EndUtc);

public interface ISlotAvailabilityService
{
    /// <summary>Bookable slots for a professional over the next <paramref name="daysAhead"/> days (local time), excluding already-taken ones.</summary>
    Task<List<AvailableSlot>> GetAvailableSlotsAsync(Guid professionalId, int daysAhead = 21);
}
