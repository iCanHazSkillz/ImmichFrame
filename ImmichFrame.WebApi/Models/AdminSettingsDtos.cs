namespace ImmichFrame.WebApi.Models;

public class AdminSettingsResponseDto
{
    public long Version { get; set; }
    public AdminManagedGeneralSettings General { get; set; } = new();
    public List<AdminAccountSettingsDto> Accounts { get; set; } = [];
    public string CustomCss { get; set; } = string.Empty;
    public bool WeatherApiKeyConfigured { get; set; }
    public string ServerTimeZone { get; set; } = string.Empty;
    public List<string> AvailableTimeZones { get; set; } = [];
    public List<string> BootstrapManagedFields { get; set; } =
    [
        "ImmichServerUrl",
        "ApiKey",
        "ApiKeyFile",
        "IMMICHFRAME_AUTH_BASIC_*",
        "AuthenticationSecret"
    ];

    public static AdminSettingsResponseDto FromSettings(
        long version,
        ServerSettings effective,
        ServerSettings bootstrap,
        string customCss,
        bool weatherApiKeyConfigured,
        string serverTimeZone,
        List<string> availableTimeZones)
    {
        ArgumentNullException.ThrowIfNull(effective);
        ArgumentNullException.ThrowIfNull(bootstrap);

        var effectiveAccounts = effective.Accounts.ToList();
        var bootstrapAccounts = bootstrap.Accounts.ToList();
        var effectiveByIdentifier = effectiveAccounts
            .Select(account => new
            {
                Identifier = ServerSettingsFactory.GetAccountIdentifier(account),
                Account = account
            })
            .ToDictionary(item => item.Identifier, item => item.Account, StringComparer.Ordinal);
        var bootstrapByIdentifier = bootstrapAccounts
            .Select(account => new
            {
                Identifier = ServerSettingsFactory.GetAccountIdentifier(account),
                Account = account
            })
            .ToDictionary(item => item.Identifier, item => item.Account, StringComparer.Ordinal);
        var orderedIdentifiers = bootstrapAccounts
            .Select(ServerSettingsFactory.GetAccountIdentifier)
            .Concat(effectiveAccounts.Select(ServerSettingsFactory.GetAccountIdentifier))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new AdminSettingsResponseDto
        {
            Version = version,
            General = AdminManagedGeneralSettings.FromGeneralSettings(effective.GeneralSettings),
            CustomCss = customCss,
            WeatherApiKeyConfigured = weatherApiKeyConfigured,
            ServerTimeZone = serverTimeZone,
            AvailableTimeZones = availableTimeZones,
            Accounts = orderedIdentifiers
                .Select((identifier, index) =>
                {
                    bootstrapByIdentifier.TryGetValue(identifier, out var bootstrapAccount);
                    effectiveByIdentifier.TryGetValue(identifier, out var currentAccount);
                    var sourceAccount = currentAccount ?? bootstrapAccount ?? throw new InvalidOperationException("Account identifier lookup unexpectedly returned no settings.");

                    return new AdminAccountSettingsDto
                    {
                        AccountIdentifier = identifier,
                        AccountIndex = index,
                        AccountLabel = $"Account {index + 1}",
                        ImmichServerUrl = bootstrapAccount?.ImmichServerUrl ?? currentAccount?.ImmichServerUrl ?? string.Empty,
                        ShowMemories = sourceAccount.ShowMemories,
                        ShowFavorites = sourceAccount.ShowFavorites,
                        ShowArchived = sourceAccount.ShowArchived,
                        ShowVideos = sourceAccount.ShowVideos,
                        ImagesFromDays = sourceAccount.ImagesFromDays,
                        ImagesFromDate = sourceAccount.ImagesFromDate,
                        ImagesUntilDate = sourceAccount.ImagesUntilDate,
                        Albums = sourceAccount.Albums.ToList(),
                        ExcludedAlbums = sourceAccount.ExcludedAlbums.ToList(),
                        People = sourceAccount.People.ToList(),
                        Tags = sourceAccount.Tags.ToList(),
                        Rating = sourceAccount.Rating
                    };
                })
                .ToList()
        };
    }
}

public class AdminSettingsUpdateRequest
{
    public AdminManagedGeneralSettings General { get; set; } = new();
    public List<AdminManagedAccountSettings> Accounts { get; set; } = [];
    public string CustomCss { get; set; } = string.Empty;
    public string? WeatherApiKey { get; set; }

    public AdminManagedSettingsDocument ToDocument()
    {
        return new AdminManagedSettingsDocument
        {
            General = General,
            Accounts = Accounts
        };
    }
}

public class AdminAccountSettingsDto : AdminManagedAccountSettings
{
    public int AccountIndex { get; set; }
    public string AccountLabel { get; set; } = string.Empty;
    public string ImmichServerUrl { get; set; } = string.Empty;
}
