namespace ImmichFrame.Core.Helpers;

public static class TimeZoneSettingsHelper
{
    public static string ResolveServerTimeZoneId()
    {
        var configuredTimeZone = NormalizeTimeZoneId(Environment.GetEnvironmentVariable("TZ"));
        if (!string.IsNullOrWhiteSpace(configuredTimeZone))
        {
            return configuredTimeZone;
        }

        return TimeZoneInfo.Local.Id;
    }

    public static TimeZoneInfo ResolveServerTimeZone()
    {
        return TimeZoneInfo.FindSystemTimeZoneById(ResolveServerTimeZoneId());
    }

    public static string ResolveCalendarTimeZoneId(string? overrideTimeZoneId)
    {
        return ResolveCalendarTimeZone(overrideTimeZoneId).Id;
    }

    public static TimeZoneInfo ResolveCalendarTimeZone(string? overrideTimeZoneId)
    {
        var normalizedOverride = NormalizeTimeZoneId(overrideTimeZoneId);
        if (!string.IsNullOrWhiteSpace(normalizedOverride))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(normalizedOverride);
        }

        return ResolveServerTimeZone();
    }

    public static string? NormalizeTimeZoneId(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(normalized).Id;
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
