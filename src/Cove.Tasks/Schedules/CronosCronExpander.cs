using Cronos;
using Microsoft.Extensions.Logging;

namespace Cove.Tasks.Schedules;

public sealed class CronosCronExpander : ICronExpander
{
    private readonly ILogger _logger;

    public CronosCronExpander(ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

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

    private System.TimeZoneInfo ResolveTimeZone(string? tz)
    {
        if (TimeZoneResolver.TryResolve(tz, out var zone))
            return zone;
        _logger.LogWarning("scheduler: time zone {tz} not resolvable on this platform, falling back to UTC", tz);
        return System.TimeZoneInfo.Utc;
    }
}
