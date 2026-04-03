using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.WebApi.Services;

public sealed class DynamicServerSettings(ISettingsSnapshotProvider settingsProvider, DynamicGeneralSettings generalSettings) : IServerSettings
{
    public IEnumerable<IAccountSettings> Accounts => settingsProvider.GetCurrentSettings().Accounts;
    public IGeneralSettings GeneralSettings => generalSettings;

    public void Validate()
    {
        settingsProvider.GetCurrentSettings().Validate();
    }
}

public sealed class DynamicGeneralSettings(ISettingsSnapshotProvider settingsProvider) : IGeneralSettings
{
    private IGeneralSettings Current => settingsProvider.GetCurrentSettings().GeneralSettings;

    public List<string> Webcalendars => Current.Webcalendars;
    public int RefreshAlbumPeopleInterval => Current.RefreshAlbumPeopleInterval;
    public string? WeatherApiKey => Current.WeatherApiKey;
    public string? WeatherLatLong => Current.WeatherLatLong;
    public string? UnitSystem => Current.UnitSystem;
    public string? Webhook => Current.Webhook;
    public string? AuthenticationSecret => Current.AuthenticationSecret;
    public int Interval => Current.Interval;
    public double TransitionDuration => Current.TransitionDuration;
    public bool DownloadImages => Current.DownloadImages;
    public int RenewImagesDuration => Current.RenewImagesDuration;
    public bool ShowClock => Current.ShowClock;
    public bool ShowWeather => Current.ShowWeather;
    public bool ShowCalendar => Current.ShowCalendar;
    public bool ShowMetadata => Current.ShowMetadata;
    public string? ClockFormat => Current.ClockFormat;
    public string? ClockDateFormat => Current.ClockDateFormat;
    public bool ShowProgressBar => Current.ShowProgressBar;
    public bool ShowPhotoDate => Current.ShowPhotoDate;
    public string? PhotoDateFormat => Current.PhotoDateFormat;
    public bool ShowPhotoTimeAgo => Current.ShowPhotoTimeAgo;
    public bool ShowImageDesc => Current.ShowImageDesc;
    public bool ShowPeopleDesc => Current.ShowPeopleDesc;
    public bool ShowPeopleAge => Current.ShowPeopleAge;
    public bool ShowTagsDesc => Current.ShowTagsDesc;
    public bool ShowAlbumName => Current.ShowAlbumName;
    public bool ShowImageLocation => Current.ShowImageLocation;
    public string? ImageLocationFormat => Current.ImageLocationFormat;
    public string? PrimaryColor => Current.PrimaryColor;
    public string? SecondaryColor => Current.SecondaryColor;
    public string Style => Current.Style;
    public string? BaseFontSize => Current.BaseFontSize;
    public string? ClockFontSize => Current.ClockFontSize;
    public string? WeatherFontSize => Current.WeatherFontSize;
    public string? CalendarFontSize => Current.CalendarFontSize;
    public string? MetadataFontSize => Current.MetadataFontSize;
    public string? ClockStyle => Current.ClockStyle;
    public string? WeatherStyle => Current.WeatherStyle;
    public string? CalendarStyle => Current.CalendarStyle;
    public string? MetadataStyle => Current.MetadataStyle;
    public string ClockPosition => Current.ClockPosition;
    public string WeatherPosition => Current.WeatherPosition;
    public string CalendarPosition => Current.CalendarPosition;
    public string MetadataPosition => Current.MetadataPosition;
    public List<string> WidgetStackOrder => Current.WidgetStackOrder;
    public bool ShowWeatherLocation => Current.ShowWeatherLocation;
    public bool ShowWeatherDescription => Current.ShowWeatherDescription;
    public string? WeatherIconUrl => Current.WeatherIconUrl;
    public bool ImageZoom => Current.ImageZoom;
    public bool ImagePan => Current.ImagePan;
    public bool ImageFill => Current.ImageFill;
    public bool PlayAudio => Current.PlayAudio;
    public string Layout => Current.Layout;
    public string Language => Current.Language;

    public void Validate()
    {
        Current.Validate();
    }
}
