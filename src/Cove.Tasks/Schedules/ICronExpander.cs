namespace Cove.Tasks.Schedules;

public interface ICronExpander
{
    string? ComputeNextFire(string cronExpression, System.DateTimeOffset baseTime, string? tz);
}

public sealed class HandRolledCronExpander : ICronExpander
{
    public string? ComputeNextFire(string cronExpression, System.DateTimeOffset baseTime, string? tz)
    {
        var fields = cronExpression.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
            return null;

        if (!TryParseField(fields[0], 0, 59, out var minutes) ||
            !TryParseField(fields[1], 0, 23, out var hours) ||
            !TryParseField(fields[2], 1, 31, out var daysOfMonth) ||
            !TryParseField(fields[3], 1, 12, out var months) ||
            !TryParseField(fields[4], 0, 6, out var daysOfWeek))
            return null;

        var zone = ResolveTimeZone(tz);
        var localBase = System.TimeZoneInfo.ConvertTime(baseTime, zone);
        var candidate = new System.DateTimeOffset(localBase.Year, localBase.Month, localBase.Day, localBase.Hour, localBase.Minute, 0, localBase.Offset).AddMinutes(1);

        for (int i = 0; i < 525600; i++)
        {
            if (minutes.Contains(candidate.Minute) &&
                hours.Contains(candidate.Hour) &&
                daysOfMonth.Contains(candidate.Day) &&
                months.Contains(candidate.Month) &&
                daysOfWeek.Contains((int)candidate.DayOfWeek))
            {
                var localResult = new System.DateTimeOffset(candidate.Year, candidate.Month, candidate.Day, candidate.Hour, candidate.Minute, 0, localBase.Offset);
                return localResult.UtcDateTime.ToString("o");
            }
            candidate = candidate.AddMinutes(1);
        }
        return null;
    }

    private static bool TryParseField(string field, int min, int max, out System.Collections.Generic.HashSet<int> values)
    {
        values = new System.Collections.Generic.HashSet<int>();
        if (field == "*")
        {
            for (int v = min; v <= max; v++) values.Add(v);
            return true;
        }
        foreach (var part in field.Split(','))
        {
            if (part.Contains('/'))
            {
                var stepParts = part.Split('/');
                if (stepParts.Length != 2) return false;
                int stepStart = stepParts[0] == "*" ? min : 0;
                if (stepParts[0] != "*" && !int.TryParse(stepParts[0], out stepStart)) return false;
                if (!int.TryParse(stepParts[1], out var step) || step <= 0) return false;
                for (int v = stepStart; v <= max; v += step) values.Add(v);
            }
            else if (part.Contains('-'))
            {
                var rangeParts = part.Split('-');
                if (rangeParts.Length != 2) return false;
                if (!int.TryParse(rangeParts[0], out var rangeStart) || !int.TryParse(rangeParts[1], out var rangeEnd)) return false;
                if (rangeStart < min || rangeEnd > max || rangeStart > rangeEnd) return false;
                for (int v = rangeStart; v <= rangeEnd; v++) values.Add(v);
            }
            else
            {
                if (!int.TryParse(part, out var v) || v < min || v > max) return false;
                values.Add(v);
            }
        }
        return values.Count > 0;
    }

    private static System.TimeZoneInfo ResolveTimeZone(string? tz)
    {
        if (string.IsNullOrWhiteSpace(tz))
            return System.TimeZoneInfo.Utc;
        try { return System.TimeZoneInfo.FindSystemTimeZoneById(tz); }
        catch { return System.TimeZoneInfo.Utc; }
    }
}
