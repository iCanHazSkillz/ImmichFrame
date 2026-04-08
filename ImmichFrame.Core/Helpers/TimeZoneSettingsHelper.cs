using TimeZoneConverter;

namespace ImmichFrame.Core.Helpers;

public static class TimeZoneSettingsHelper
{
    public static IReadOnlyList<string> GetAvailableTimeZoneIds()
    {
        return TZConvert.KnownIanaTimeZoneNames
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToList();
    }

    public static string ResolveServerTimeZoneId()
    {
        var configuredTimeZone = NormalizeTimeZoneId(Environment.GetEnvironmentVariable("TZ"));
        if (!string.IsNullOrWhiteSpace(configuredTimeZone))
        {
            return configuredTimeZone;
        }

        return NormalizeTimeZoneId(TimeZoneInfo.Local.Id) ?? TimeZoneInfo.Local.Id;
    }

    public static TimeZoneInfo ResolveServerTimeZone()
    {
        return TZConvert.GetTimeZoneInfo(ResolveServerTimeZoneId());
    }

    public static string ResolveCalendarTimeZoneId(string? overrideTimeZoneId)
    {
        if (TryResolveCalendarTimeZoneId(overrideTimeZoneId, out var resolvedTimeZoneId))
        {
            return resolvedTimeZoneId;
        }

        throw new TimeZoneNotFoundException(
            $"The calendar timezone '{overrideTimeZoneId}' is not a recognized timezone identifier.");
    }

    public static TimeZoneInfo ResolveCalendarTimeZone(string? overrideTimeZoneId)
    {
        return TZConvert.GetTimeZoneInfo(ResolveCalendarTimeZoneId(overrideTimeZoneId));
    }

    public static bool TryResolveCalendarTimeZoneId(string? overrideTimeZoneId, out string resolvedTimeZoneId)
    {
        if (string.IsNullOrWhiteSpace(overrideTimeZoneId))
        {
            resolvedTimeZoneId = ResolveServerTimeZoneId();
            return true;
        }

        var normalizedOverride = NormalizeTimeZoneId(overrideTimeZoneId);
        if (!string.IsNullOrWhiteSpace(normalizedOverride))
        {
            resolvedTimeZoneId = normalizedOverride;
            return true;
        }

        resolvedTimeZoneId = string.Empty;
        return false;
    }

    public static string? NormalizeTimeZoneId(string? value)
    {
        var candidate = value?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        if (TZConvert.TryWindowsToIana(candidate, out var windowsMapped) &&
            !string.IsNullOrWhiteSpace(windowsMapped))
        {
            return windowsMapped;
        }

        if (TZConvert.TryIanaToWindows(candidate, out var windowsId) &&
            TZConvert.TryWindowsToIana(windowsId, out var canonicalIana) &&
            !string.IsNullOrWhiteSpace(canonicalIana))
        {
            return canonicalIana;
        }

        if (TZConvert.TryGetTimeZoneInfo(candidate, out var timeZoneInfo))
        {
            if (TZConvert.TryWindowsToIana(timeZoneInfo.Id, out var mappedFromResolvedId) &&
                !string.IsNullOrWhiteSpace(mappedFromResolvedId))
            {
                return mappedFromResolvedId;
            }

            if (TZConvert.TryIanaToWindows(timeZoneInfo.Id, out var windowsFromResolvedId) &&
                TZConvert.TryWindowsToIana(windowsFromResolvedId, out var canonicalFromResolvedId) &&
                !string.IsNullOrWhiteSpace(canonicalFromResolvedId))
            {
                return canonicalFromResolvedId;
            }

            return timeZoneInfo.Id;
        }

        return null;
    }
}
