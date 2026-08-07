namespace CentralPsi.Web.Services;

public interface ITimeZoneService
{
    DateTime ToLocal(DateTime utc);
    DateTime ToUtc(DateTime local);
    TimeZoneInfo TimeZone { get; }
}
