using ImmichFrame.Core.Interfaces;
using System.Text.RegularExpressions;

namespace ImmichFrame.WebApi.Models;

public class AdminManagedSettingsDocument
{
    public AdminManagedGeneralSettings General { get; set; } = new();
    public List<AdminManagedAccountSettings> Accounts { get; set; } = [];

    public void Normalize()
    {
        General ??= new AdminManagedGeneralSettings();
        Accounts ??= [];
        General.Normalize();

        foreach (var account in Accounts)
        {
            account.Normalize();
        }
    }

    public static AdminManagedSettingsDocument FromServerSettings(IServerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new AdminManagedSettingsDocument
        {
            General = AdminManagedGeneralSettings.FromGeneralSettings(settings.GeneralSettings),
            Accounts = settings.Accounts.Select(AdminManagedAccountSettings.FromAccountSettings).ToList()
        };
    }
}

public class AdminManagedGeneralSettings
{
    private static readonly Regex EncodedCalendarSegmentPattern = new("%+(?=[0-9A-Fa-f]{2})", RegexOptions.Compiled);
    public bool DownloadImages { get; set; } = false;
    public string Language { get; set; } = "en";
    public string? ImageLocationFormat { get; set; } = "City,State,Country";
    public string? PhotoDateFormat { get; set; } = "MM/dd/yyyy";
    public int Interval { get; set; } = 45;
    public double TransitionDuration { get; set; } = 1;
    public bool ShowClock { get; set; } = true;
    public bool ShowWeather { get; set; } = true;
    public bool ShowCalendar { get; set; } = true;
    public bool ShowMetadata { get; set; } = true;
    public string? ClockFormat { get; set; } = "hh:mm";
    public string? ClockDateFormat { get; set; } = "eee, MMM d";
    public bool ShowProgressBar { get; set; } = true;
    public bool ShowPhotoDate { get; set; } = true;
    public bool ShowPhotoTimeAgo { get; set; } = false;
    public bool ShowImageDesc { get; set; } = true;
    public bool ShowPeopleDesc { get; set; } = true;
    public bool ShowPeopleAge { get; set; } = false;
    public bool ShowTagsDesc { get; set; } = true;
    public bool ShowAlbumName { get; set; } = true;
    public bool ShowImageLocation { get; set; } = true;
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string Style { get; set; } = "none";
    public string? BaseFontSize { get; set; }
    public string? ClockFontSize { get; set; }
    public string? WeatherFontSize { get; set; }
    public string? CalendarFontSize { get; set; }
    public string? MetadataFontSize { get; set; }
    public string? ClockStyle { get; set; }
    public string? WeatherStyle { get; set; }
    public string? CalendarStyle { get; set; }
    public string? MetadataStyle { get; set; }
    public string ClockPosition { get; set; } = "bottom-left";
    public string WeatherPosition { get; set; } = "bottom-left";
    public string CalendarPosition { get; set; } = "top-right";
    public string MetadataPosition { get; set; } = "bottom-right";
    public List<string> WidgetStackOrder { get; set; } = ["clock", "weather", "metadata", "calendar"];
    public bool ShowWeatherLocation { get; set; } = true;
    public bool ShowWeatherDescription { get; set; } = true;
    public string? WeatherIconUrl { get; set; } = "https://openweathermap.org/img/wn/{IconId}.png";
    public bool ImageZoom { get; set; } = true;
    public bool ImagePan { get; set; } = false;
    public bool ImageFill { get; set; } = false;
    public bool PlayAudio { get; set; } = false;
    public string Layout { get; set; } = "splitview";
    public int RenewImagesDuration { get; set; } = 30;
    public List<string> Webcalendars { get; set; } = [];
    public int RefreshAlbumPeopleInterval { get; set; } = 12;
    public string? WeatherApiKey { get; set; } = string.Empty;
    public string? UnitSystem { get; set; } = "imperial";
    public string? WeatherLatLong { get; set; } = "40.7128,74.0060";
    public string? Webhook { get; set; }

    public void Normalize()
    {
        Webcalendars = (Webcalendars ?? [])
            .Select(NormalizeCalendarUrl)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        Language ??= "en";
        if (!ShowPeopleDesc)
        {
            ShowPeopleAge = false;
        }
        Style = NormalizeStyle(Style) ?? "none";
        Layout ??= "splitview";
        ClockStyle = NormalizeStyle(ClockStyle);
        WeatherStyle = NormalizeStyle(WeatherStyle);
        CalendarStyle = NormalizeStyle(CalendarStyle);
        MetadataStyle = NormalizeStyle(MetadataStyle);
        (ShowClock, ClockPosition) = ApplyLegacyHiddenPosition(
            ShowClock,
            NormalizePosition(ClockPosition, "bottom-left"),
            "bottom-left");
        (ShowWeather, WeatherPosition) = ApplyLegacyHiddenPosition(
            ShowWeather,
            NormalizePosition(WeatherPosition, "bottom-left"),
            "bottom-left");
        (ShowCalendar, CalendarPosition) = ApplyLegacyHiddenPosition(
            ShowCalendar,
            NormalizePosition(CalendarPosition, "top-right"),
            "top-right");
        (ShowMetadata, MetadataPosition) = ApplyLegacyHiddenPosition(
            ShowMetadata,
            NormalizePosition(MetadataPosition, "bottom-right"),
            "bottom-right");
        WidgetStackOrder = NormalizeWidgetStackOrder(WidgetStackOrder);
    }

    public void ApplyTo(GeneralSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Normalize();

        settings.DownloadImages = DownloadImages;
        settings.Language = Language;
        settings.ImageLocationFormat = ImageLocationFormat;
        settings.PhotoDateFormat = PhotoDateFormat;
        settings.Interval = Interval;
        settings.TransitionDuration = TransitionDuration;
        settings.ShowClock = ShowClock;
        settings.ShowWeather = ShowWeather;
        settings.ShowCalendar = ShowCalendar;
        settings.ShowMetadata = ShowMetadata;
        settings.ClockFormat = ClockFormat;
        settings.ClockDateFormat = ClockDateFormat;
        settings.ShowProgressBar = ShowProgressBar;
        settings.ShowPhotoDate = ShowPhotoDate;
        settings.ShowPhotoTimeAgo = ShowPhotoTimeAgo;
        settings.ShowImageDesc = ShowImageDesc;
        settings.ShowPeopleDesc = ShowPeopleDesc;
        settings.ShowPeopleAge = ShowPeopleAge;
        settings.ShowTagsDesc = ShowTagsDesc;
        settings.ShowAlbumName = ShowAlbumName;
        settings.ShowImageLocation = ShowImageLocation;
        settings.PrimaryColor = PrimaryColor;
        settings.SecondaryColor = SecondaryColor;
        settings.Style = Style;
        settings.BaseFontSize = BaseFontSize;
        settings.ClockFontSize = ClockFontSize;
        settings.WeatherFontSize = WeatherFontSize;
        settings.CalendarFontSize = CalendarFontSize;
        settings.MetadataFontSize = MetadataFontSize;
        settings.ClockStyle = ClockStyle;
        settings.WeatherStyle = WeatherStyle;
        settings.CalendarStyle = CalendarStyle;
        settings.MetadataStyle = MetadataStyle;
        settings.ClockPosition = ClockPosition;
        settings.WeatherPosition = WeatherPosition;
        settings.CalendarPosition = CalendarPosition;
        settings.MetadataPosition = MetadataPosition;
        settings.WidgetStackOrder = WidgetStackOrder.ToList();
        settings.ShowWeatherLocation = ShowWeatherLocation;
        settings.ShowWeatherDescription = ShowWeatherDescription;
        settings.WeatherIconUrl = WeatherIconUrl;
        settings.ImageZoom = ImageZoom;
        settings.ImagePan = ImagePan;
        settings.ImageFill = ImageFill;
        settings.PlayAudio = PlayAudio;
        settings.Layout = Layout;
        settings.RenewImagesDuration = RenewImagesDuration;
        settings.Webcalendars = Webcalendars.ToList();
        settings.RefreshAlbumPeopleInterval = RefreshAlbumPeopleInterval;
        settings.WeatherApiKey = WeatherApiKey;
        settings.UnitSystem = UnitSystem;
        settings.WeatherLatLong = WeatherLatLong;
        settings.Webhook = Webhook;
    }

    public static AdminManagedGeneralSettings FromGeneralSettings(IGeneralSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new AdminManagedGeneralSettings
        {
            DownloadImages = settings.DownloadImages,
            Language = settings.Language,
            ImageLocationFormat = settings.ImageLocationFormat,
            PhotoDateFormat = settings.PhotoDateFormat,
            Interval = settings.Interval,
            TransitionDuration = settings.TransitionDuration,
            ShowClock = settings.ShowClock,
            ShowWeather = settings.ShowWeather,
            ShowCalendar = settings.ShowCalendar,
            ShowMetadata = settings.ShowMetadata,
            ClockFormat = settings.ClockFormat,
            ClockDateFormat = settings.ClockDateFormat,
            ShowProgressBar = settings.ShowProgressBar,
            ShowPhotoDate = settings.ShowPhotoDate,
            ShowPhotoTimeAgo = settings.ShowPhotoTimeAgo,
            ShowImageDesc = settings.ShowImageDesc,
            ShowPeopleDesc = settings.ShowPeopleDesc,
            ShowPeopleAge = settings.ShowPeopleAge,
            ShowTagsDesc = settings.ShowTagsDesc,
            ShowAlbumName = settings.ShowAlbumName,
            ShowImageLocation = settings.ShowImageLocation,
            PrimaryColor = settings.PrimaryColor,
            SecondaryColor = settings.SecondaryColor,
            Style = settings.Style,
            BaseFontSize = settings.BaseFontSize,
            ClockFontSize = settings.ClockFontSize,
            WeatherFontSize = settings.WeatherFontSize,
            CalendarFontSize = settings.CalendarFontSize,
            MetadataFontSize = settings.MetadataFontSize,
            ClockStyle = settings.ClockStyle,
            WeatherStyle = settings.WeatherStyle,
            CalendarStyle = settings.CalendarStyle,
            MetadataStyle = settings.MetadataStyle,
            ClockPosition = settings.ClockPosition,
            WeatherPosition = settings.WeatherPosition,
            CalendarPosition = settings.CalendarPosition,
            MetadataPosition = settings.MetadataPosition,
            WidgetStackOrder = settings.WidgetStackOrder.ToList(),
            ShowWeatherLocation = settings.ShowWeatherLocation,
            ShowWeatherDescription = settings.ShowWeatherDescription,
            WeatherIconUrl = settings.WeatherIconUrl,
            ImageZoom = settings.ImageZoom,
            ImagePan = settings.ImagePan,
            ImageFill = settings.ImageFill,
            PlayAudio = settings.PlayAudio,
            Layout = settings.Layout,
            RenewImagesDuration = settings.RenewImagesDuration,
            Webcalendars = settings.Webcalendars.ToList(),
            RefreshAlbumPeopleInterval = settings.RefreshAlbumPeopleInterval,
            WeatherApiKey = settings.WeatherApiKey,
            UnitSystem = settings.UnitSystem,
            WeatherLatLong = settings.WeatherLatLong,
            Webhook = settings.Webhook
        };
    }

    private static string NormalizePosition(string? value, string fallback)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is "top-left" or "top-right" or "bottom-left" or "bottom-right" or "hidden"
            ? normalized
            : fallback;
    }

    private static (bool ShowWidget, string Position) ApplyLegacyHiddenPosition(
        bool showWidget,
        string position,
        string fallback)
    {
        if (!string.Equals(position, "hidden", StringComparison.OrdinalIgnoreCase))
        {
            return (showWidget, position);
        }

        return (false, fallback);
    }

    private static string? NormalizeStyle(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is "none" or "solid" or "transition" or "blur"
            ? normalized
            : null;
    }

    private static string NormalizeCalendarUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return EncodedCalendarSegmentPattern.Replace(value.Trim(), "%");
    }

    private static List<string> NormalizeWidgetStackOrder(List<string>? value)
    {
        var ordered = new List<string>();
        foreach (var widget in value ?? [])
        {
            var normalized = widget?.Trim().ToLowerInvariant();
            if (normalized is "clock" or "weather" or "metadata" or "calendar" && !ordered.Contains(normalized))
            {
                ordered.Add(normalized);
            }
        }

        foreach (var widget in new[] { "clock", "weather", "metadata", "calendar" })
        {
            if (!ordered.Contains(widget))
            {
                ordered.Add(widget);
            }
        }

        return ordered;
    }
}

public class AdminManagedAccountSettings
{
    public string AccountIdentifier { get; set; } = string.Empty;
    public bool ShowMemories { get; set; } = false;
    public bool ShowFavorites { get; set; } = false;
    public bool ShowArchived { get; set; } = false;
    public bool ShowVideos { get; set; } = false;
    public int? ImagesFromDays { get; set; }
    public DateTime? ImagesFromDate { get; set; }
    public DateTime? ImagesUntilDate { get; set; }
    public List<Guid> Albums { get; set; } = [];
    public List<Guid> ExcludedAlbums { get; set; } = [];
    public List<Guid> People { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public int? Rating { get; set; }

    public void Normalize()
    {
        AccountIdentifier = AccountIdentifier?.Trim() ?? string.Empty;
        Albums ??= [];
        ExcludedAlbums ??= [];
        People ??= [];
        Tags ??= [];
    }

    public void ApplyTo(ServerAccountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Normalize();

        settings.ShowMemories = ShowMemories;
        settings.ShowFavorites = ShowFavorites;
        settings.ShowArchived = ShowArchived;
        settings.ShowVideos = ShowVideos;
        settings.ImagesFromDays = ImagesFromDays;
        settings.ImagesFromDate = ImagesFromDate;
        settings.ImagesUntilDate = ImagesUntilDate;
        settings.Albums = Albums.ToList();
        settings.ExcludedAlbums = ExcludedAlbums.ToList();
        settings.People = People.ToList();
        settings.Tags = Tags.ToList();
        settings.Rating = Rating;
    }

    public static AdminManagedAccountSettings FromAccountSettings(IAccountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new AdminManagedAccountSettings
        {
            AccountIdentifier = ServerSettingsFactory.GetAccountIdentifier(settings),
            ShowMemories = settings.ShowMemories,
            ShowFavorites = settings.ShowFavorites,
            ShowArchived = settings.ShowArchived,
            ShowVideos = settings.ShowVideos,
            ImagesFromDays = settings.ImagesFromDays,
            ImagesFromDate = settings.ImagesFromDate,
            ImagesUntilDate = settings.ImagesUntilDate,
            Albums = settings.Albums.ToList(),
            ExcludedAlbums = settings.ExcludedAlbums.ToList(),
            People = settings.People.ToList(),
            Tags = settings.Tags.ToList(),
            Rating = settings.Rating
        };
    }
}
