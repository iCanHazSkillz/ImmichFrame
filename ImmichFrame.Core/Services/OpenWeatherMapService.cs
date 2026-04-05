using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using System.Globalization;

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
        var snapshot = _settingsProvider.GetCurrentSnapshot();
        var settings = snapshot.Settings.GeneralSettings;
        if (!settings.ShowWeather || string.IsNullOrWhiteSpace(settings.WeatherApiKey))
        {
            return null;
        }

        var cache = GetCache(snapshot.Version);
        var cachedWeather = await cache.GetOrAddAsync("weather", async () =>
        {
            var weatherLatLong = settings.WeatherLatLong;
            var weatherLat = 0f;
            var weatherLong = 0f;

            if (!string.IsNullOrWhiteSpace(weatherLatLong))
            {
                var parts = weatherLatLong.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length > 0)
                {
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out weatherLat);
                }

                if (parts.Length > 1)
                {
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out weatherLong);
                }
            }

            var weather = await GetWeather(weatherLat, weatherLong);

            return new CachedWeather(weather);
        });

        return cachedWeather.Weather;
    }

    public async Task<IWeather?> GetWeather(double latitude, double longitude)
    {
        var settings = _settingsProvider.GetCurrentSnapshot().Settings.GeneralSettings;
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

    private ApiCache GetCache(long version)
    {
        if (_cacheVersion == version)
        {
            return _weatherCache;
        }

        ApiCache? oldCache = null;
        ApiCache? newCache = null;
        lock (_sync)
        {
            if (_cacheVersion == version)
            {
                return _weatherCache;
            }

            oldCache = _weatherCache;
            newCache = new ApiCache(TimeSpan.FromMinutes(5));
            _weatherCache = newCache;
            _cacheVersion = version;
        }

        oldCache?.Dispose();
        return newCache!;
    }
}
