namespace ImmichFrame.Core.Helpers;

public static class CalendarSettingsLimits
{
    public const int DefaultLookaheadDays = 0;
    public const int MaxLookaheadDays = 7;
    public const int DefaultMaxEvents = 5;
    public const int MinMaxEvents = 1;
    public const int MaxMaxEvents = 10;
    public const string DefaultSortDirection = "ascending";
    public const string DescendingSortDirection = "descending";

    public static int NormalizeLookaheadDays(int value)
        => Math.Clamp(value, DefaultLookaheadDays, MaxLookaheadDays);

    public static int NormalizeMaxEvents(int value)
        => Math.Clamp(value, MinMaxEvents, MaxMaxEvents);

    public static string NormalizeSortDirection(string? value)
    {
        return string.Equals(value?.Trim(), DescendingSortDirection, StringComparison.OrdinalIgnoreCase)
            ? DescendingSortDirection
            : DefaultSortDirection;
    }

    public static bool IsDescendingSortDirection(string? value)
        => string.Equals(NormalizeSortDirection(value), DescendingSortDirection, StringComparison.Ordinal);
}
