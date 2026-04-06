using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Ical.Net;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.WebApi.Helpers;
using Microsoft.Extensions.Logging;

public class IcalCalendarService : ICalendarService
{
    private static readonly Regex EncodedCalendarSegmentPattern = new("%+(?=[0-9A-Fa-f]{2})", RegexOptions.Compiled);
    private readonly ISettingsSnapshotProvider _settingsProvider;
    private readonly ILogger<IcalCalendarService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly object _sync = new();
    private ApiCache _appointmentCache = new(TimeSpan.FromMinutes(15));
    private long _cacheVersion = -1;

    public IcalCalendarService(ISettingsSnapshotProvider settingsProvider, ILogger<IcalCalendarService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<IAppointment>> GetAppointments()
    {
        var snapshot = _settingsProvider.GetCurrentSnapshot();
        var settings = snapshot.Settings.GeneralSettings;
        var cache = GetCache(snapshot.Version);
        using var cacheLease = cache.AcquireLease();
        return await cache.GetOrAddAsync("appointments", async () =>
        {
            var appointments = new List<IAppointment>();

            List<(string? auth, string url)> cals = settings.Webcalendars.Select<string, (string? auth, string url)?>(x =>
            {
                try
                {
                    var normalizedUrl = NormalizeCalendarUrl(x);
                    if (string.IsNullOrWhiteSpace(normalizedUrl))
                    {
                        return null;
                    }

                    var uri = new Uri(normalizedUrl.Replace("webcal://", "https://"));
                    if (!string.IsNullOrEmpty(uri.UserInfo))
                    {
                        var url = uri.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo, UriFormat.UriEscaped);
                        return (Uri.UnescapeDataString(uri.UserInfo), url);
                    }
                    return (null, normalizedUrl);
                }
                catch (UriFormatException)
                {
                    _logger.LogError($"Invalid calendar URL: '{x}'");
                    return null;
                }
            }).Where(x => x != null).Select(x => x!.Value).ToList();

            var icals = await GetCalendars(cals);

            foreach (var ical in icals)
            {
                var calendar = Calendar.Load(ical);

                appointments.AddRange(calendar.GetOccurrences(DateTime.Today, DateTime.Today.AddDays(1)).Select(x => x.ToAppointment()));
            }

            return appointments;
        });
    }

    public async Task<List<string>> GetCalendars(IEnumerable<(string? auth, string url)> calendars)
    {
        var icals = new List<string>();
        var client = _httpClientFactory.CreateClient();

        foreach (var calendar in calendars)
        {
            _logger.LogDebug($"Loading calendar: {(calendar.auth != null ? "[authenticated]" : "no auth")} - {calendar.url}");

            string httpUrl = calendar.url.Replace("webcal://", "https://");
            httpUrl = NormalizeCalendarUrl(httpUrl);

            using var request = new HttpRequestMessage(HttpMethod.Get, httpUrl);

            if (!string.IsNullOrEmpty(calendar.auth))
            {
                var byteArray = Encoding.UTF8.GetBytes(calendar.auth);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }

            using var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                icals.Add(await response.Content.ReadAsStringAsync());
            }
            else
            {
                _logger.LogError($"Failed to load calendar data from '{httpUrl}' (Status: {response.StatusCode})");
            }
        }

        return icals;
    }

    private static string NormalizeCalendarUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return EncodedCalendarSegmentPattern.Replace(value.Trim(), "%");
    }

    private ApiCache GetCache(long version)
    {
        if (_cacheVersion == version)
        {
            return _appointmentCache;
        }

        ApiCache? oldCache = null;
        ApiCache? newCache = null;
        lock (_sync)
        {
            if (_cacheVersion == version)
            {
                return _appointmentCache;
            }

            oldCache = _appointmentCache;
            newCache = new ApiCache(TimeSpan.FromMinutes(15));
            _appointmentCache = newCache;
            _cacheVersion = version;
        }

        oldCache?.Dispose();
        return newCache!;
    }
}
