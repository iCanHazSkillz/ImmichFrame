using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.WebApi.Models;

public static class ServerSettingsFactory
{
    public static ServerSettings Clone(IServerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new ServerSettings
        {
            GeneralSettingsImpl = Clone(settings.GeneralSettings),
            AccountsImpl = settings.Accounts.Select(Clone).ToList()
        };
    }

    public static GeneralSettings Clone(IGeneralSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new GeneralSettings
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
            Webhook = settings.Webhook,
            AuthenticationSecret = settings.AuthenticationSecret
        };
    }

    public static ServerAccountSettings Clone(IAccountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new ServerAccountSettings
        {
            ImmichServerUrl = settings.ImmichServerUrl,
            // ApiKeyFile values are intentionally flattened into ApiKey so the
            // validated bootstrap snapshot can be re-validated safely.
            ApiKey = settings.ApiKey,
            ApiKeyFile = null,
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
