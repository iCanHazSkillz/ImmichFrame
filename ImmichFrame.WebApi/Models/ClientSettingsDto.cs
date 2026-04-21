using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Helpers;

namespace ImmichFrame.WebApi.Models;

public class ClientSettingsDto
{
    public int Interval { get; set; }
    public double TransitionDuration { get; set; }
    public bool DownloadImages { get; set; }
    public int RenewImagesDuration { get; set; }
    public bool ShowClock { get; set; }
    public bool ShowWeather { get; set; }
    public bool ShowCalendar { get; set; }
    public bool ShowMetadata { get; set; }
    public string? ClockFormat { get; set; }
    public string? ClockDateFormat { get; set; }
    public bool ShowPhotoDate { get; set; }
    public bool ShowProgressBar { get; set; }
    public string? PhotoDateFormat { get; set; }
    public bool ShowPhotoTimeAgo { get; set; }
    public bool ShowImageDesc { get; set; }
    public bool ShowPeopleDesc { get; set; }
    public bool ShowPeopleAge { get; set; }
    public bool ShowTagsDesc { get; set; }
    public bool ShowAlbumName { get; set; }
    public bool ShowImageLocation { get; set; }
    public string? ImageLocationFormat { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string Style { get; set; } = "none";
    public string? BaseFontSize { get; set; }
    public string? ClockFontSize { get; set; }
    public string? WeatherFontSize { get; set; }
    public string? CalendarFontSize { get; set; }
    public string? MetadataFontSize { get; set; }
    public string? CalendarTimeZone { get; set; }
    public string? CalendarDateFormat { get; set; }
    public int CalendarLookaheadDays { get; set; }
    public int CalendarMaxEvents { get; set; }
    public string CalendarSortDirection { get; set; } = CalendarSettingsLimits.DefaultSortDirection;
    public string? ClockStyle { get; set; }
    public string? WeatherStyle { get; set; }
    public string? CalendarStyle { get; set; }
    public string? MetadataStyle { get; set; }
    public string ClockPosition { get; set; } = "bottom-left";
    public string WeatherPosition { get; set; } = "bottom-left";
    public string CalendarPosition { get; set; } = "top-right";
    public string MetadataPosition { get; set; } = "bottom-right";
    public List<string> WidgetStackOrder { get; set; } = [];
    public bool ShowWeatherLocation { get; set; }
    public bool ShowWeatherDescription { get; set; }
    public string? WeatherIconUrl { get; set; }
    public bool ImageZoom { get; set; }
    public bool ImagePan { get; set; }
    public bool ImageFill { get; set; }
    public bool PlayAudio { get; set; }
    public string Layout { get; set; } = "splitview";
    public string Language { get; set; } = "en";
    public List<string> Webcalendars { get; set; } = [];

    public static ClientSettingsDto FromGeneralSettings(IGeneralSettings generalSettings)
    {
        ClientSettingsDto dto = new ClientSettingsDto();
        dto.Interval = generalSettings.Interval;
        dto.TransitionDuration = generalSettings.TransitionDuration;
        dto.DownloadImages = generalSettings.DownloadImages;
        dto.RenewImagesDuration = generalSettings.RenewImagesDuration;
        dto.ShowClock = generalSettings.ShowClock;
        dto.ShowWeather = generalSettings.ShowWeather;
        dto.ShowCalendar = generalSettings.ShowCalendar;
        dto.ShowMetadata = generalSettings.ShowMetadata;
        dto.ClockFormat = generalSettings.ClockFormat;
        dto.ClockDateFormat = generalSettings.ClockDateFormat;
        dto.ShowPhotoDate = generalSettings.ShowPhotoDate;
        dto.ShowProgressBar = generalSettings.ShowProgressBar;
        dto.PhotoDateFormat = generalSettings.PhotoDateFormat;
        dto.ShowPhotoTimeAgo = generalSettings.ShowPhotoTimeAgo;
        dto.ShowImageDesc = generalSettings.ShowImageDesc;
        dto.ShowPeopleDesc = generalSettings.ShowPeopleDesc;
        dto.ShowPeopleAge = generalSettings.ShowPeopleAge;
        dto.ShowTagsDesc = generalSettings.ShowTagsDesc;
        dto.ShowAlbumName = generalSettings.ShowAlbumName;
        dto.ShowImageLocation = generalSettings.ShowImageLocation;
        dto.ImageLocationFormat = generalSettings.ImageLocationFormat;
        dto.PrimaryColor = generalSettings.PrimaryColor;
        dto.SecondaryColor = generalSettings.SecondaryColor;
        dto.Style = generalSettings.Style;
        dto.BaseFontSize = generalSettings.BaseFontSize;
        dto.ClockFontSize = generalSettings.ClockFontSize;
        dto.WeatherFontSize = generalSettings.WeatherFontSize;
        dto.CalendarFontSize = generalSettings.CalendarFontSize;
        dto.MetadataFontSize = generalSettings.MetadataFontSize;
        dto.CalendarTimeZone = TimeZoneSettingsHelper.ResolveCalendarTimeZoneId(generalSettings.CalendarTimeZone);
        dto.CalendarDateFormat = generalSettings.CalendarDateFormat;
        dto.CalendarLookaheadDays = CalendarSettingsLimits.NormalizeLookaheadDays(generalSettings.CalendarLookaheadDays);
        dto.CalendarMaxEvents = CalendarSettingsLimits.NormalizeMaxEvents(generalSettings.CalendarMaxEvents);
        dto.CalendarSortDirection = CalendarSettingsLimits.NormalizeSortDirection(generalSettings.CalendarSortDirection);
        dto.ClockStyle = generalSettings.ClockStyle;
        dto.WeatherStyle = generalSettings.WeatherStyle;
        dto.CalendarStyle = generalSettings.CalendarStyle;
        dto.MetadataStyle = generalSettings.MetadataStyle;
        dto.ClockPosition = generalSettings.ClockPosition;
        dto.WeatherPosition = generalSettings.WeatherPosition;
        dto.CalendarPosition = generalSettings.CalendarPosition;
        dto.MetadataPosition = generalSettings.MetadataPosition;
        dto.WidgetStackOrder = generalSettings.WidgetStackOrder.ToList();
        dto.ShowWeatherLocation = generalSettings.ShowWeatherLocation;
        dto.ShowWeatherDescription = generalSettings.ShowWeatherDescription;
        dto.WeatherIconUrl = generalSettings.WeatherIconUrl;
        dto.ImageZoom = generalSettings.ImageZoom;
        dto.ImagePan = generalSettings.ImagePan;
        dto.ImageFill = generalSettings.ImageFill;
        dto.PlayAudio = generalSettings.PlayAudio;
        dto.Layout = generalSettings.Layout;
        dto.Language = generalSettings.Language;
        dto.Webcalendars = generalSettings.Webcalendars.ToList();
        return dto;
    }
}
