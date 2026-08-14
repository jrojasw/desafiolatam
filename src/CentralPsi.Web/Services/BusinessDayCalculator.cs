namespace CentralPsi.Web.Services;

/// <summary>Counts weekdays (Mon-Fri) between two UTC instants - used to track the 3-business-day commitment
/// for paying professionals after a session. Doesn't account for Chilean public holidays.</summary>
public static class BusinessDayCalculator
{
    public static int BusinessDaysElapsed(DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc <= fromUtc) return 0;

        var days = 0;
        var cursor = fromUtc.Date;
        var end = toUtc.Date;
        while (cursor < end)
        {
            cursor = cursor.AddDays(1);
            if (cursor.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                days++;
            }
        }
        return days;
    }
}
