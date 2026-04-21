using System.Security.Cryptography;
using System.Text;
using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.WebApi.Models;

public static class ServerSettingsFactory
{
    public static ServerSettings Clone(IServerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var clone = new ServerSettings
        {
            GeneralSettingsImpl = Clone(settings.GeneralSettings),
            AccountsImpl = settings.Accounts.Select(Clone).ToList()
        };

        EnsureAccountIdentifiers(clone);
        return clone;
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
            CalendarTimeZone = settings.CalendarTimeZone,
            CalendarDateFormat = settings.CalendarDateFormat,
            CalendarLookaheadDays = settings.CalendarLookaheadDays,
            CalendarMaxEvents = settings.CalendarMaxEvents,
            CalendarSortDirection = settings.CalendarSortDirection,
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
            AccountIdentifier = GetAccountIdentifier(settings),
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

    public static string BuildAccountIdentifier(IAccountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var input = NormalizeServerUrl(settings.ImmichServerUrl);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    public static string BuildLegacyAccountIdentifier(IAccountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // ApiKeyFile is intentionally omitted here because bootstrap cloning flattens
        // file-backed credentials into ApiKey before runtime settings are compared.
        var input = $"{NormalizeServerUrl(settings.ImmichServerUrl)}\n{settings.ApiKey}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    public static string GetAccountIdentifier(IAccountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings is ServerAccountSettings serverAccount && !string.IsNullOrWhiteSpace(serverAccount.AccountIdentifier))
        {
            return serverAccount.AccountIdentifier;
        }

        return BuildAccountIdentifier(settings);
    }

    public static void EnsureAccountIdentifiers(ServerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var accounts = settings.AccountsImpl.ToList();
        var accountEntries = accounts
            .Select((account, index) => new
            {
                Account = account,
                OriginalIndex = index,
                BaseIdentifier = string.IsNullOrWhiteSpace(account.AccountIdentifier)
                    ? BuildAccountIdentifier(account)
                    : account.AccountIdentifier.Trim(),
                StableSortKey = BuildStableAccountDisambiguator(account)
            })
            .ToList();

        foreach (var group in accountEntries.GroupBy(entry => entry.BaseIdentifier, StringComparer.Ordinal))
        {
            var orderedEntries = group
                .OrderBy(entry => entry.StableSortKey, StringComparer.Ordinal)
                .ThenBy(entry => entry.OriginalIndex)
                .ToList();

            if (orderedEntries.Count == 0)
            {
                continue;
            }

            orderedEntries[0].Account.AccountIdentifier = group.Key;

            for (var collisionIndex = 1; collisionIndex < orderedEntries.Count; collisionIndex++)
            {
                orderedEntries[collisionIndex].Account.AccountIdentifier = $"{group.Key}:{collisionIndex}";
            }
        }

        settings.AccountsImpl = accounts;
    }

    private static string BuildStableAccountDisambiguator(IAccountSettings account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var builder = new StringBuilder();
        builder.Append(NormalizeServerUrl(account.ImmichServerUrl)).Append('\n');
        builder.Append(account.ShowMemories).Append('\n');
        builder.Append(account.ShowFavorites).Append('\n');
        builder.Append(account.ShowArchived).Append('\n');
        builder.Append(account.ShowVideos).Append('\n');
        builder.Append(account.ImagesFromDays?.ToString() ?? string.Empty).Append('\n');
        builder.Append(account.ImagesFromDate?.ToString("O") ?? string.Empty).Append('\n');
        builder.Append(account.ImagesUntilDate?.ToString("O") ?? string.Empty).Append('\n');
        builder.Append(string.Join(",", account.Albums.OrderBy(id => id))).Append('\n');
        builder.Append(string.Join(",", account.ExcludedAlbums.OrderBy(id => id))).Append('\n');
        builder.Append(string.Join(",", account.People.OrderBy(id => id))).Append('\n');
        builder.Append(string.Join(",", account.Tags.OrderBy(tag => tag, StringComparer.Ordinal))).Append('\n');
        builder.Append(account.Rating?.ToString() ?? string.Empty);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes);
    }

    private static string NormalizeServerUrl(string? value)
    {
        return value?.Trim().TrimEnd('/').ToLowerInvariant() ?? string.Empty;
    }
}
