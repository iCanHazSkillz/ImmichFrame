using System.Text.Json.Serialization;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.WebApi.Helpers;
using YamlDotNet.Serialization;

namespace ImmichFrame.WebApi.Models;

public class ServerSettings : IServerSettings, IConfigSettable
{
    [YamlMember(Alias = "General")]
    [JsonPropertyName("General")]
    public GeneralSettings? GeneralSettingsImpl { get; set; }

    [YamlMember(Alias = "Accounts")]
    [JsonPropertyName("Accounts")]
    public IEnumerable<ServerAccountSettings> AccountsImpl { get; set; } = Array.Empty<ServerAccountSettings>();

    //Covariance not allowed on interface impls
    [JsonIgnore]
    [YamlIgnore]
    public IGeneralSettings GeneralSettings => GeneralSettingsImpl ?? new GeneralSettings();

    [JsonIgnore]
    [YamlIgnore]
    public IEnumerable<IAccountSettings> Accounts => AccountsImpl;

    public void Validate()
    {
        GeneralSettings.Validate();

        foreach (var account in Accounts)
        {
            account.ValidateAndInitialize();
        }
    }
}

public class GeneralSettings : IGeneralSettings, IConfigSettable
{
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
    public string? CalendarTimeZone { get; set; }
    public string? CalendarDateFormat { get; set; }
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
    public List<string> Webcalendars { get; set; } = new();
    public int RefreshAlbumPeopleInterval { get; set; } = 12;
    public string? WeatherApiKey { get; set; } = string.Empty;
    public string? UnitSystem { get; set; } = "imperial";
    public string? WeatherLatLong { get; set; } = "40.7128,74.0060";
    public string? Webhook { get; set; }
    public string? AuthenticationSecret { get; set; }

    public void Validate() { }
}

public class ServerAccountSettings : IAccountSettings, IConfigSettable
{
    public string AccountIdentifier { get; set; } = string.Empty;
    public string ImmichServerUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? ApiKeyFile { get; set; } = null;
    public bool ShowMemories { get; set; } = false;
    public bool ShowFavorites { get; set; } = false;
    public bool ShowArchived { get; set; } = false;
    public bool ShowVideos { get; set; } = false;

    public int? ImagesFromDays { get; set; }
    public DateTime? ImagesFromDate { get; set; }
    public DateTime? ImagesUntilDate { get; set; }
    public List<Guid> Albums { get; set; } = new();
    public List<Guid> ExcludedAlbums { get; set; } = new();
    public List<Guid> People { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public int? Rating { get; set; }

    public void ValidateAndInitialize()
    {
        if (!string.IsNullOrWhiteSpace(ApiKeyFile))
        {
            if (!string.IsNullOrWhiteSpace(ApiKey))
            {
                throw new Exception("Cannot specify both ApiKey and ApiKeyFile. Please provide only one.");
            }
            ApiKey = File.ReadAllText(ApiKeyFile).Trim();
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Either ApiKey or ApiKeyFile must be provided.");
        }
    }
}
