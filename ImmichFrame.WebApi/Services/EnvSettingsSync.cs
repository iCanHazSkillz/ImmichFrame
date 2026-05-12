using System.Globalization;
using ImmichFrame.WebApi.Models;

namespace ImmichFrame.WebApi.Services;

public class EnvSettingsSyncOptions
{
    public string? EnvFilePath { get; init; }
}

public sealed record EnvSettingsImportResult(bool WeatherApiKeyChanged, string? WeatherApiKey);

public interface IEnvSettingsSync
{
    bool IsConfigured { get; }
    bool Exists();
    DateTimeOffset? GetLastWriteTimeUtc();
    EnvSettingsImportResult ImportInto(AdminManagedSettingsDocument settings);
    void Save(AdminManagedSettingsDocument settings, string? weatherApiKey = null);
}

public sealed class EnvSettingsSync(EnvSettingsSyncOptions options) : IEnvSettingsSync
{
    private static readonly HashSet<string> SyncKeys = new(StringComparer.Ordinal)
    {
        "DownloadImages",
        "Language",
        "ImageLocationFormat",
        "PhotoDateFormat",
        "Interval",
        "TransitionDuration",
        "ShowClock",
        "ShowWeather",
        "ShowCalendar",
        "ShowMetadata",
        "ClockFormat",
        "ClockDateFormat",
        "ShowProgressBar",
        "ShowPhotoDate",
        "ShowPhotoTimeAgo",
        "ShowImageDesc",
        "ShowPeopleDesc",
        "ShowPeopleAge",
        "ShowTagsDesc",
        "ShowAlbumName",
        "ShowImageLocation",
        "PrimaryColor",
        "SecondaryColor",
        "Style",
        "BaseFontSize",
        "ClockFontSize",
        "WeatherFontSize",
        "CalendarFontSize",
        "MetadataFontSize",
        "CalendarTimeZone",
        "CalendarDateFormat",
        "CalendarLookaheadDays",
        "CalendarMaxEvents",
        "CalendarSortDirection",
        "ShowWeatherLocation",
        "ShowWeatherDescription",
        "WeatherIconUrl",
        "ImageZoom",
        "ImagePan",
        "ImageFill",
        "PlayAudio",
        "Layout",
        "RenewImagesDuration",
        "Webcalendars",
        "RefreshAlbumPeopleInterval",
        "UnitSystem",
        "WeatherLatLong",
        "WeatherApiKey",
        "ShowMemories",
        "ShowFavorites",
        "ShowArchived",
        "ShowVideos",
        "ImagesFromDays",
        "ImagesFromDate",
        "ImagesUntilDate",
        "Albums",
        "ExcludedAlbums",
        "People",
        "Tags",
        "Rating"
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.EnvFilePath);

    public bool Exists()
    {
        return IsConfigured && File.Exists(EnsurePathConfigured());
    }

    public DateTimeOffset? GetLastWriteTimeUtc()
    {
        if (!Exists())
        {
            return null;
        }

        return new DateTimeOffset(File.GetLastWriteTimeUtc(EnsurePathConfigured()), TimeSpan.Zero);
    }

    public EnvSettingsImportResult ImportInto(AdminManagedSettingsDocument settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!Exists())
        {
            return new EnvSettingsImportResult(false, null);
        }

        settings.Normalize();
        var values = EnvFile.Load(EnsurePathConfigured()).Values;
        var firstAccount = settings.Accounts.FirstOrDefault();
        var weatherApiKeyChanged = false;
        string? weatherApiKey = null;

        foreach (var (key, value) in values)
        {
            switch (key)
            {
                case "DownloadImages":
                    settings.General.DownloadImages = ParseBool(key, value);
                    break;
                case "Language":
                    settings.General.Language = value;
                    break;
                case "ImageLocationFormat":
                    settings.General.ImageLocationFormat = EmptyToNull(value);
                    break;
                case "PhotoDateFormat":
                    settings.General.PhotoDateFormat = EmptyToNull(value);
                    break;
                case "Interval":
                    settings.General.Interval = ParseInt(key, value);
                    break;
                case "TransitionDuration":
                    settings.General.TransitionDuration = ParseDouble(key, value);
                    break;
                case "ShowClock":
                    settings.General.ShowClock = ParseBool(key, value);
                    break;
                case "ShowWeather":
                    settings.General.ShowWeather = ParseBool(key, value);
                    break;
                case "ShowCalendar":
                    settings.General.ShowCalendar = ParseBool(key, value);
                    break;
                case "ShowMetadata":
                    settings.General.ShowMetadata = ParseBool(key, value);
                    break;
                case "ClockFormat":
                    settings.General.ClockFormat = EmptyToNull(value);
                    break;
                case "ClockDateFormat":
                    settings.General.ClockDateFormat = EmptyToNull(value);
                    break;
                case "ShowProgressBar":
                    settings.General.ShowProgressBar = ParseBool(key, value);
                    break;
                case "ShowPhotoDate":
                    settings.General.ShowPhotoDate = ParseBool(key, value);
                    break;
                case "ShowPhotoTimeAgo":
                    settings.General.ShowPhotoTimeAgo = ParseBool(key, value);
                    break;
                case "ShowImageDesc":
                    settings.General.ShowImageDesc = ParseBool(key, value);
                    break;
                case "ShowPeopleDesc":
                    settings.General.ShowPeopleDesc = ParseBool(key, value);
                    break;
                case "ShowPeopleAge":
                    settings.General.ShowPeopleAge = ParseBool(key, value);
                    break;
                case "ShowTagsDesc":
                    settings.General.ShowTagsDesc = ParseBool(key, value);
                    break;
                case "ShowAlbumName":
                    settings.General.ShowAlbumName = ParseBool(key, value);
                    break;
                case "ShowImageLocation":
                    settings.General.ShowImageLocation = ParseBool(key, value);
                    break;
                case "PrimaryColor":
                    settings.General.PrimaryColor = EmptyToNull(value);
                    break;
                case "SecondaryColor":
                    settings.General.SecondaryColor = EmptyToNull(value);
                    break;
                case "Style":
                    settings.General.Style = value;
                    break;
                case "BaseFontSize":
                    settings.General.BaseFontSize = EmptyToNull(value);
                    break;
                case "ClockFontSize":
                    settings.General.ClockFontSize = EmptyToNull(value);
                    break;
                case "WeatherFontSize":
                    settings.General.WeatherFontSize = EmptyToNull(value);
                    break;
                case "CalendarFontSize":
                    settings.General.CalendarFontSize = EmptyToNull(value);
                    break;
                case "MetadataFontSize":
                    settings.General.MetadataFontSize = EmptyToNull(value);
                    break;
                case "CalendarTimeZone":
                    settings.General.CalendarTimeZone = EmptyToNull(value);
                    break;
                case "CalendarDateFormat":
                    settings.General.CalendarDateFormat = EmptyToNull(value);
                    break;
                case "CalendarLookaheadDays":
                    settings.General.CalendarLookaheadDays = ParseInt(key, value);
                    break;
                case "CalendarMaxEvents":
                    settings.General.CalendarMaxEvents = ParseInt(key, value);
                    break;
                case "CalendarSortDirection":
                    settings.General.CalendarSortDirection = value;
                    break;
                case "ShowWeatherLocation":
                    settings.General.ShowWeatherLocation = ParseBool(key, value);
                    break;
                case "ShowWeatherDescription":
                    settings.General.ShowWeatherDescription = ParseBool(key, value);
                    break;
                case "WeatherIconUrl":
                    settings.General.WeatherIconUrl = EmptyToNull(value);
                    break;
                case "ImageZoom":
                    settings.General.ImageZoom = ParseBool(key, value);
                    break;
                case "ImagePan":
                    settings.General.ImagePan = ParseBool(key, value);
                    break;
                case "ImageFill":
                    settings.General.ImageFill = ParseBool(key, value);
                    break;
                case "PlayAudio":
                    settings.General.PlayAudio = ParseBool(key, value);
                    break;
                case "Layout":
                    settings.General.Layout = value;
                    break;
                case "RenewImagesDuration":
                    settings.General.RenewImagesDuration = ParseInt(key, value);
                    break;
                case "Webcalendars":
                    settings.General.Webcalendars = ParseStringList(value);
                    break;
                case "RefreshAlbumPeopleInterval":
                    settings.General.RefreshAlbumPeopleInterval = ParseInt(key, value);
                    break;
                case "UnitSystem":
                    settings.General.UnitSystem = EmptyToNull(value);
                    break;
                case "WeatherLatLong":
                    settings.General.WeatherLatLong = EmptyToNull(value);
                    break;
                case "WeatherApiKey":
                    weatherApiKeyChanged = true;
                    weatherApiKey = value;
                    break;
                case "ShowMemories" when firstAccount is not null:
                    firstAccount.ShowMemories = ParseBool(key, value);
                    break;
                case "ShowFavorites" when firstAccount is not null:
                    firstAccount.ShowFavorites = ParseBool(key, value);
                    break;
                case "ShowArchived" when firstAccount is not null:
                    firstAccount.ShowArchived = ParseBool(key, value);
                    break;
                case "ShowVideos" when firstAccount is not null:
                    firstAccount.ShowVideos = ParseBool(key, value);
                    break;
                case "ImagesFromDays" when firstAccount is not null:
                    firstAccount.ImagesFromDays = ParseNullableInt(key, value);
                    break;
                case "ImagesFromDate" when firstAccount is not null:
                    firstAccount.ImagesFromDate = ParseNullableDateTime(key, value);
                    break;
                case "ImagesUntilDate" when firstAccount is not null:
                    firstAccount.ImagesUntilDate = ParseNullableDateTime(key, value);
                    break;
                case "Albums" when firstAccount is not null:
                    firstAccount.Albums = ParseGuidList(key, value);
                    break;
                case "ExcludedAlbums" when firstAccount is not null:
                    firstAccount.ExcludedAlbums = ParseGuidList(key, value);
                    break;
                case "People" when firstAccount is not null:
                    firstAccount.People = ParseGuidList(key, value);
                    break;
                case "Tags" when firstAccount is not null:
                    firstAccount.Tags = ParseStringList(value);
                    break;
                case "Rating" when firstAccount is not null:
                    firstAccount.Rating = ParseNullableInt(key, value);
                    break;
            }
        }

        settings.Normalize();
        return new EnvSettingsImportResult(weatherApiKeyChanged, weatherApiKey);
    }

    public void Save(AdminManagedSettingsDocument settings, string? weatherApiKey = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!IsConfigured)
        {
            return;
        }

        settings.Normalize();
        var envFile = EnvFile.LoadOrCreate(EnsurePathConfigured());
        var values = BuildValues(settings, weatherApiKey);

        foreach (var (key, value) in values)
        {
            envFile.Set(key, value);
        }

        envFile.Save(EnsurePathConfigured());
    }

    private string EnsurePathConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.EnvFilePath))
        {
            throw new InvalidOperationException("Environment settings sync file path is not configured.");
        }

        return options.EnvFilePath;
    }

    private static Dictionary<string, string> BuildValues(AdminManagedSettingsDocument settings, string? weatherApiKey)
    {
        var general = settings.General;
        var firstAccount = settings.Accounts.FirstOrDefault();
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DownloadImages"] = FormatBool(general.DownloadImages),
            ["Language"] = general.Language,
            ["ImageLocationFormat"] = FormatNullable(general.ImageLocationFormat),
            ["PhotoDateFormat"] = FormatNullable(general.PhotoDateFormat),
            ["Interval"] = FormatInt(general.Interval),
            ["TransitionDuration"] = FormatDouble(general.TransitionDuration),
            ["ShowClock"] = FormatBool(general.ShowClock),
            ["ShowWeather"] = FormatBool(general.ShowWeather),
            ["ShowCalendar"] = FormatBool(general.ShowCalendar),
            ["ShowMetadata"] = FormatBool(general.ShowMetadata),
            ["ClockFormat"] = FormatNullable(general.ClockFormat),
            ["ClockDateFormat"] = FormatNullable(general.ClockDateFormat),
            ["ShowProgressBar"] = FormatBool(general.ShowProgressBar),
            ["ShowPhotoDate"] = FormatBool(general.ShowPhotoDate),
            ["ShowPhotoTimeAgo"] = FormatBool(general.ShowPhotoTimeAgo),
            ["ShowImageDesc"] = FormatBool(general.ShowImageDesc),
            ["ShowPeopleDesc"] = FormatBool(general.ShowPeopleDesc),
            ["ShowPeopleAge"] = FormatBool(general.ShowPeopleAge),
            ["ShowTagsDesc"] = FormatBool(general.ShowTagsDesc),
            ["ShowAlbumName"] = FormatBool(general.ShowAlbumName),
            ["ShowImageLocation"] = FormatBool(general.ShowImageLocation),
            ["PrimaryColor"] = FormatNullable(general.PrimaryColor),
            ["SecondaryColor"] = FormatNullable(general.SecondaryColor),
            ["Style"] = general.Style,
            ["BaseFontSize"] = FormatNullable(general.BaseFontSize),
            ["ClockFontSize"] = FormatNullable(general.ClockFontSize),
            ["WeatherFontSize"] = FormatNullable(general.WeatherFontSize),
            ["CalendarFontSize"] = FormatNullable(general.CalendarFontSize),
            ["MetadataFontSize"] = FormatNullable(general.MetadataFontSize),
            ["CalendarTimeZone"] = FormatNullable(general.CalendarTimeZone),
            ["CalendarDateFormat"] = FormatNullable(general.CalendarDateFormat),
            ["CalendarLookaheadDays"] = FormatInt(general.CalendarLookaheadDays),
            ["CalendarMaxEvents"] = FormatInt(general.CalendarMaxEvents),
            ["CalendarSortDirection"] = general.CalendarSortDirection,
            ["ShowWeatherLocation"] = FormatBool(general.ShowWeatherLocation),
            ["ShowWeatherDescription"] = FormatBool(general.ShowWeatherDescription),
            ["WeatherIconUrl"] = FormatNullable(general.WeatherIconUrl),
            ["ImageZoom"] = FormatBool(general.ImageZoom),
            ["ImagePan"] = FormatBool(general.ImagePan),
            ["ImageFill"] = FormatBool(general.ImageFill),
            ["PlayAudio"] = FormatBool(general.PlayAudio),
            ["Layout"] = general.Layout,
            ["RenewImagesDuration"] = FormatInt(general.RenewImagesDuration),
            ["Webcalendars"] = FormatStringList(general.Webcalendars),
            ["RefreshAlbumPeopleInterval"] = FormatInt(general.RefreshAlbumPeopleInterval),
            ["UnitSystem"] = FormatNullable(general.UnitSystem),
            ["WeatherLatLong"] = FormatNullable(general.WeatherLatLong)
        };

        if (!string.IsNullOrWhiteSpace(weatherApiKey))
        {
            values["WeatherApiKey"] = weatherApiKey.Trim();
        }

        if (firstAccount is not null)
        {
            values["ShowMemories"] = FormatBool(firstAccount.ShowMemories);
            values["ShowFavorites"] = FormatBool(firstAccount.ShowFavorites);
            values["ShowArchived"] = FormatBool(firstAccount.ShowArchived);
            values["ShowVideos"] = FormatBool(firstAccount.ShowVideos);
            values["ImagesFromDays"] = FormatNullableInt(firstAccount.ImagesFromDays);
            values["ImagesFromDate"] = FormatNullableDateTime(firstAccount.ImagesFromDate);
            values["ImagesUntilDate"] = FormatNullableDateTime(firstAccount.ImagesUntilDate);
            values["Albums"] = FormatGuidList(firstAccount.Albums);
            values["ExcludedAlbums"] = FormatGuidList(firstAccount.ExcludedAlbums);
            values["People"] = FormatGuidList(firstAccount.People);
            values["Tags"] = FormatStringList(firstAccount.Tags);
            values["Rating"] = FormatNullableInt(firstAccount.Rating);
        }

        return values;
    }

    private static string? EmptyToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool ParseBool(string key, string value)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new FormatException($"{key} must be a boolean value.");
    }

    private static int ParseInt(string key, string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new FormatException($"{key} must be an integer value.");
    }

    private static int? ParseNullableInt(string key, string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : ParseInt(key, value);
    }

    private static double ParseDouble(string key, string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new FormatException($"{key} must be a number.");
    }

    private static DateTime? ParseNullableDateTime(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.Date;
        }

        throw new FormatException($"{key} must be a date value.");
    }

    private static List<Guid> ParseGuidList(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return SplitList(value)
            .Select(item => Guid.TryParse(item, out var parsed)
                ? parsed
                : throw new FormatException($"{key} contains an invalid UUID value."))
            .ToList();
    }

    private static List<string> ParseStringList(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? [] : SplitList(value).ToList();
    }

    private static IEnumerable<string> SplitList(string value)
    {
        return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string FormatBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string FormatInt(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatNullableInt(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string FormatDouble(double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatNullable(string? value)
    {
        return value ?? string.Empty;
    }

    private static string FormatNullableDateTime(DateTime? value)
    {
        return value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string FormatGuidList(IEnumerable<Guid> values)
    {
        return string.Join(",", values);
    }

    private static string FormatStringList(IEnumerable<string> values)
    {
        return string.Join(",", values);
    }

    private sealed class EnvFile
    {
        private readonly List<string> _lines;
        private readonly Dictionary<string, int> _keyLineIndexes = new(StringComparer.Ordinal);

        private EnvFile(IEnumerable<string> lines)
        {
            _lines = lines.ToList();
            Values = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var index = 0; index < _lines.Count; index++)
            {
                if (!TryParseSettingLine(_lines[index], out var key, out var value) || !SyncKeys.Contains(key))
                {
                    continue;
                }

                Values[key] = value;
                _keyLineIndexes[key] = index;
            }
        }

        public Dictionary<string, string> Values { get; }

        public static EnvFile Load(string path)
        {
            return new EnvFile(File.ReadAllLines(path));
        }

        public static EnvFile LoadOrCreate(string path)
        {
            if (Directory.Exists(path))
            {
                throw new IOException($"The configured environment settings sync path is a directory: {path}");
            }

            return File.Exists(path) ? Load(path) : new EnvFile([]);
        }

        public void Set(string key, string value)
        {
            if (!SyncKeys.Contains(key))
            {
                return;
            }

            var nextLine = $"{key}={value}";
            if (_keyLineIndexes.TryGetValue(key, out var index))
            {
                _lines[index] = nextLine;
                Values[key] = value;
                return;
            }

            _keyLineIndexes[key] = _lines.Count;
            Values[key] = value;
            _lines.Add(nextLine);
        }

        public void Save(string path)
        {
            if (Directory.Exists(path))
            {
                throw new IOException($"The configured environment settings sync path is a directory: {path}");
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            WriteAtomically(path, string.Join(Environment.NewLine, _lines) + Environment.NewLine);
        }

        private static bool TryParseSettingLine(string line, out string key, out string value)
        {
            key = string.Empty;
            value = string.Empty;

            var trimmedStart = line.TrimStart();
            if (trimmedStart.Length == 0 || trimmedStart.StartsWith('#'))
            {
                return false;
            }

            var index = line.IndexOf('=');
            if (index <= 0)
            {
                return false;
            }

            key = line[..index].Trim();
            value = line[(index + 1)..];
            return key.Length > 0;
        }

        private static void WriteAtomically(string path, string content)
        {
            var tempPath = Path.Combine(
                Path.GetDirectoryName(path) ?? string.Empty,
                $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(content);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    var backupPath = $"{tempPath}.bak";
                    try
                    {
                        File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                        File.Delete(backupPath);
                    }
                    catch (IOException ex) when (CanFallbackToInPlaceWrite(ex))
                    {
                        if (File.Exists(backupPath))
                        {
                            File.Delete(backupPath);
                        }

                        WriteInPlace(tempPath, path);
                    }
                    catch
                    {
                        if (File.Exists(backupPath))
                        {
                            File.Delete(backupPath);
                        }

                        throw;
                    }
                }
                else
                {
                    File.Move(tempPath, path, overwrite: true);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static bool CanFallbackToInPlaceWrite(IOException ex)
        {
            return ex.Message.Contains("Device or resource busy", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("Invalid cross-device link", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("Cross-device link", StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteInPlace(string sourcePath, string destinationPath)
        {
            using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            source.CopyTo(destination);
            destination.Flush(true);
        }
    }
}
