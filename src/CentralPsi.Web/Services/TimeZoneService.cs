using CentralPsi.Web.Options;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Services;

public class TimeZoneService : ITimeZoneService
{
    public TimeZoneInfo TimeZone { get; }

    public TimeZoneService(IOptions<AppOptions> options)
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
    }

    public DateTime ToLocal(DateTime utc)
    {
        var utcKind = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utcKind, TimeZone);
    }

    public DateTime ToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, TimeZone);
    }
}
