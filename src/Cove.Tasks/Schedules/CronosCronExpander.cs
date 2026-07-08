using Cronos;

namespace Cove.Tasks.Schedules;

public sealed class CronosCronExpander : ICronExpander
{
    public string? ComputeNextFire(string cronExpression, System.DateTimeOffset baseTime, string? tz)
    {
        CronExpression expr;
        try { expr = CronExpression.Parse(cronExpression, CronFormat.Standard); }
        catch (System.Exception) { return null; }

        try
        {
            var zone = ResolveTimeZone(tz);
            var next = expr.GetNextOccurrence(baseTime, zone);
            if (next is null) return null;
            return next.Value.UtcDateTime.ToString("o");
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    private static System.TimeZoneInfo ResolveTimeZone(string? tz)
    {
        if (string.IsNullOrWhiteSpace(tz))
            return System.TimeZoneInfo.Utc;
        try { return System.TimeZoneInfo.FindSystemTimeZoneById(tz); }
        catch { return System.TimeZoneInfo.Utc; }
    }
}
