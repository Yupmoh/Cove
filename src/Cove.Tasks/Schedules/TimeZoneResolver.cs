namespace Cove.Tasks.Schedules;

public static class TimeZoneResolver
{
    public static bool TryResolve(string? tz, out System.TimeZoneInfo zone)
    {
        zone = System.TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(tz))
            return true;
        try
        {
            zone = System.TimeZoneInfo.FindSystemTimeZoneById(tz);
            return true;
        }
        catch (System.TimeZoneNotFoundException) { }
        catch (System.InvalidTimeZoneException) { }

        if (System.OperatingSystem.IsWindows()
            && System.TimeZoneInfo.TryConvertIanaIdToWindowsId(tz, out var windowsId))
        {
            try
            {
                zone = System.TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                return true;
            }
            catch (System.TimeZoneNotFoundException) { }
            catch (System.InvalidTimeZoneException) { }
        }
        return false;
    }
}
