using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;

public class OpenWeatherMapService : IWeatherService
{
    private sealed record CachedWeather(IWeather? Weather);

    private readonly ISettingsSnapshotProvider _settingsProvider;
    private readonly object _sync = new();
    private ApiCache _weatherCache = new(TimeSpan.FromMinutes(5));
    private long _cacheVersion = -1;

    public OpenWeatherMapService(ISettingsSnapshotProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    public async Task<IWeather?> GetWeather()
    {
        var settings = _settingsProvider.GetCurrentSettings().GeneralSettings;
        if (!settings.ShowWeather || string.IsNullOrWhiteSpace(settings.WeatherApiKey))
        {
            return null;
        }

        var cache = GetCache();
        var cachedWeather = await cache.GetOrAddAsync("weather", async () =>
        {
            var weatherLatLong = settings.WeatherLatLong;

            var weatherLat = !string.IsNullOrWhiteSpace(weatherLatLong) ? float.Parse(weatherLatLong!.Split(',')[0]) : 0f;
            var weatherLong = !string.IsNullOrWhiteSpace(weatherLatLong) ? float.Parse(weatherLatLong!.Split(',')[1]) : 0f;

            var weather = await GetWeather(weatherLat, weatherLong);

            return new CachedWeather(weather);
        });

        return cachedWeather.Weather;
    }

    public async Task<IWeather?> GetWeather(double latitude, double longitude)
    {
        var settings = _settingsProvider.GetCurrentSettings().GeneralSettings;
        if (!settings.ShowWeather || string.IsNullOrWhiteSpace(settings.WeatherApiKey))
        {
            return null;
        }

        OpenWeatherMap.OpenWeatherMapOptions options = new OpenWeatherMap.OpenWeatherMapOptions
        {
            ApiKey = settings.WeatherApiKey,
            UnitSystem = settings.UnitSystem,
            Language = settings.Language,
        };

        try
        {
            OpenWeatherMap.IOpenWeatherMapService openWeatherMapService = new OpenWeatherMap.OpenWeatherMapService(options);
            var weatherInfo = await openWeatherMapService.GetCurrentWeatherAsync(latitude, longitude);

            return weatherInfo.ToWeather();
        }
        catch
        {
            //do nothing and return null
        }

        return null;
    }

    private ApiCache GetCache()
    {
        var version = _settingsProvider.GetCurrentVersion();
        if (_cacheVersion == version)
        {
            return _weatherCache;
        }

        lock (_sync)
        {
            if (_cacheVersion == version)
            {
                return _weatherCache;
            }

            _weatherCache.Dispose();
            _weatherCache = new ApiCache(TimeSpan.FromMinutes(5));
            _cacheVersion = version;
            return _weatherCache;
        }
    }
}
