using System.Text.RegularExpressions;

namespace ImmichFrame.Core.Helpers;

public static partial class CalendarUrlNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return EncodedCalendarSegmentPattern().Replace(value.Trim(), "%");
    }

    [GeneratedRegex("%+(?=[0-9A-Fa-f]{2})", RegexOptions.Compiled)]
    private static partial Regex EncodedCalendarSegmentPattern();
}
