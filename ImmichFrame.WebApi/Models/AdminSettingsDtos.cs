namespace ImmichFrame.WebApi.Models;

public class AdminSettingsResponseDto
{
    public long Version { get; set; }
    public AdminManagedGeneralSettings General { get; set; } = new();
    public List<AdminAccountSettingsDto> Accounts { get; set; } = [];
    public string CustomCss { get; set; } = string.Empty;
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
        string customCss)
    {
        ArgumentNullException.ThrowIfNull(effective);
        ArgumentNullException.ThrowIfNull(bootstrap);

        return new AdminSettingsResponseDto
        {
            Version = version,
            General = AdminManagedGeneralSettings.FromGeneralSettings(effective.GeneralSettings),
            CustomCss = customCss,
            Accounts = effective.Accounts
                .Zip(bootstrap.Accounts, (current, bootstrapAccount) => new { Current = current, Bootstrap = bootstrapAccount })
                .Select((pair, index) => new AdminAccountSettingsDto
                {
                    AccountIndex = index,
                    AccountLabel = $"Account {index + 1}",
                    ImmichServerUrl = pair.Bootstrap.ImmichServerUrl,
                    ShowMemories = pair.Current.ShowMemories,
                    ShowFavorites = pair.Current.ShowFavorites,
                    ShowArchived = pair.Current.ShowArchived,
                    ShowVideos = pair.Current.ShowVideos,
                    ImagesFromDays = pair.Current.ImagesFromDays,
                    ImagesFromDate = pair.Current.ImagesFromDate,
                    ImagesUntilDate = pair.Current.ImagesUntilDate,
                    Albums = pair.Current.Albums.ToList(),
                    ExcludedAlbums = pair.Current.ExcludedAlbums.ToList(),
                    People = pair.Current.People.ToList(),
                    Tags = pair.Current.Tags.ToList(),
                    Rating = pair.Current.Rating
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
