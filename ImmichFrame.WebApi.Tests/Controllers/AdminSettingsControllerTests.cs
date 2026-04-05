using System.Collections;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ImmichFrame.WebApi.Models;
using ImmichFrame.WebApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.Controllers;

[TestFixture]
public class AdminSettingsControllerTests
{
    private const string TestImmichServerUrl = "http://mock-immich-server.com";
    private const string TestApiKey = "test-api-key";
    private WebApplicationFactory<Program> _factory = null!;
    private string _tempAppDataPath = null!;

    [SetUp]
    public void Setup()
    {
        _tempAppDataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempAppDataPath);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var generalSettings = new GeneralSettings
                    {
                        AuthenticationSecret = "test-secret",
                        Interval = 45,
                        ShowClock = true,
                        ShowWeather = true,
                        ShowCalendar = true,
                        ClockFormat = "hh:mm",
                        ClockDateFormat = "eee, MMM d",
                        PhotoDateFormat = "MM/dd/yyyy",
                        ImageLocationFormat = "City,State,Country",
                        Layout = "splitview",
                        Language = "en"
                    };

                    var accountSettings = new ServerAccountSettings
                    {
                        ImmichServerUrl = TestImmichServerUrl,
                        ApiKey = TestApiKey,
                        ShowFavorites = false,
                        ShowMemories = false
                    };

                    var serverSettings = new ServerSettings
                    {
                        GeneralSettingsImpl = generalSettings,
                        AccountsImpl = new List<ServerAccountSettings> { accountSettings }
                    };

                    services.AddSingleton(new BootstrapServerSettingsHolder(serverSettings));
                    services.AddSingleton(new AdminManagedSettingsStoreOptions
                    {
                        StorePath = Path.Combine(_tempAppDataPath, "admin-settings.json")
                    });
                    services.AddSingleton(new CustomCssStoreOptions
                    {
                        StorePath = Path.Combine(_tempAppDataPath, "custom.css"),
                        FallbackPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "static", "custom.css")
                    });
                    services.AddSingleton<IAdminBasicAuthService>(_ =>
                        new AdminBasicAuthService(new Hashtable
                        {
                            ["IMMICHFRAME_AUTH_BASIC_ADMIN_USER"] = "admin",
                            ["IMMICHFRAME_AUTH_BASIC_ADMIN_HASH"] =
                                "{SHA}" + Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes("secret")))
                        }));
                    services.AddSingleton<IFrameSessionRegistry>(_ =>
                        new FrameSessionRegistry(
                            new FrameSessionRegistryOptions
                            {
                                DisplayNameStorePath = Path.Combine(_tempAppDataPath, "frame-session-display-names.json")
                            },
                            null,
                            null));
                });
            });
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
        if (Directory.Exists(_tempAppDataPath))
        {
            Directory.Delete(_tempAppDataPath, true);
        }
    }

    [Test]
    public async Task Get_SeedsAdminSettingsFromBootstrapConfig()
    {
        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var response = await adminClient.GetFromJsonAsync<AdminSettingsResponseDto>("/api/admin/settings");

        Assert.That(response, Is.Not.Null);
            Assert.Multiple(() =>
        {
            Assert.That(response!.General.Interval, Is.EqualTo(45));
            Assert.That(response.General.ShowClock, Is.True);
            Assert.That(response.General.ShowWeather, Is.True);
            Assert.That(response.General.ShowCalendar, Is.True);
            Assert.That(response.Accounts, Has.Count.EqualTo(1));
            Assert.That(response.Accounts[0].ImmichServerUrl, Is.EqualTo(TestImmichServerUrl));
            Assert.That(response.Accounts[0].AccountIdentifier, Is.EqualTo(CreateAccountIdentifier()));
            Assert.That(response.BootstrapManagedFields, Does.Contain("ApiKey"));
        });

        var storePath = Path.Combine(_tempAppDataPath, "admin-settings.json");
        Assert.That(File.Exists(storePath), Is.True);
    }

    [Test]
    public async Task Get_BackfillsShowWeatherWhenPersistedSettingsPredateThatField()
    {
        var storePath = Path.Combine(_tempAppDataPath, "admin-settings.json");
        await File.WriteAllTextAsync(storePath, """
        {
          "General": {
            "Interval": 45,
            "ShowClock": true,
            "ClockFormat": "hh:mm",
            "ClockDateFormat": "eee, MMM d",
            "PhotoDateFormat": "MM/dd/yyyy",
            "ImageLocationFormat": "City,State,Country",
            "Layout": "splitview",
            "Language": "en",
            "ShowProgressBar": true,
            "ShowPhotoDate": true,
            "ShowImageDesc": true,
            "ShowPeopleDesc": true,
            "ShowTagsDesc": true,
            "ShowAlbumName": true,
            "ShowImageLocation": true,
            "ShowWeatherDescription": true,
            "ImageZoom": true,
            "ImagePan": false,
            "ImageFill": false,
            "PlayAudio": false,
            "Style": "none",
            "Webcalendars": []
          },
          "Accounts": [
            {
              "ShowFavorites": false,
              "ShowMemories": false,
              "ShowArchived": false,
              "ShowVideos": false,
              "Albums": [],
              "ExcludedAlbums": [],
              "People": [],
              "Tags": []
            }
          ]
        }
        """);

        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var response = await adminClient.GetFromJsonAsync<AdminSettingsResponseDto>("/api/admin/settings");

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.General.ShowWeather, Is.True);
    }

    [Test]
    public async Task Get_MigratesLegacyHiddenPositionsToShowToggles()
    {
        var storePath = Path.Combine(_tempAppDataPath, "admin-settings.json");
        await File.WriteAllTextAsync(storePath, """
        {
          "General": {
            "ShowClock": true,
            "ShowWeather": true,
            "ShowCalendar": true,
            "ClockPosition": "hidden",
            "WeatherPosition": "hidden",
            "CalendarPosition": "hidden",
            "MetadataPosition": "hidden",
            "WidgetStackOrder": ["clock", "weather", "metadata", "calendar"]
          },
          "Accounts": [
            {
              "ShowFavorites": false,
              "ShowMemories": false,
              "ShowArchived": false,
              "ShowVideos": false,
              "Albums": [],
              "ExcludedAlbums": [],
              "People": [],
              "Tags": []
            }
          ]
        }
        """);

        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var response = await adminClient.GetFromJsonAsync<AdminSettingsResponseDto>("/api/admin/settings");

        Assert.That(response, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(response!.General.ShowClock, Is.False);
            Assert.That(response.General.ShowWeather, Is.False);
            Assert.That(response.General.ShowCalendar, Is.False);
            Assert.That(response.General.ShowMetadata, Is.False);
            Assert.That(response.General.ClockPosition, Is.EqualTo("bottom-left"));
            Assert.That(response.General.WeatherPosition, Is.EqualTo("bottom-left"));
            Assert.That(response.General.CalendarPosition, Is.EqualTo("top-right"));
            Assert.That(response.General.MetadataPosition, Is.EqualTo("bottom-right"));
        });
    }

    [Test]
    public async Task Update_PersistsSettingsQueuesRefreshAndServesCustomCss()
    {
        const string expectedCustomCss = "#progressbar { visibility: hidden }";
        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var frameClient = _factory.CreateClient();
        frameClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-secret");

        var snapshot = new FrameSessionSnapshotDto
        {
            PlaybackState = FramePlaybackState.Playing,
            Status = FrameSessionStatus.Active,
            History = []
        };

        var putSessionResponse = await frameClient.PutAsJsonAsync("/api/frame-sessions/frame-settings", snapshot);
        putSessionResponse.EnsureSuccessStatusCode();

        var updateRequest = new AdminSettingsUpdateRequest
        {
            General = new AdminManagedGeneralSettings
            {
                Interval = 90,
                ShowClock = false,
                ShowWeather = false,
                ShowCalendar = true,
                ClockFormat = "HH:mm",
                ClockDateFormat = "eee, MMM d",
                PhotoDateFormat = "yyyy-MM-dd",
                ImageLocationFormat = "City,Country",
                Layout = "single",
                Language = "en",
                ShowProgressBar = true,
                ShowPhotoDate = true,
                ShowImageDesc = true,
                ShowPeopleDesc = true,
                ShowTagsDesc = true,
                ShowAlbumName = true,
                ShowImageLocation = true,
                ShowWeatherDescription = true,
                ImageZoom = true,
                ImagePan = false,
                ImageFill = false,
                PlayAudio = false,
                Style = "none",
                Webcalendars = ["https://calendar.example.com/basic.ics"]
            },
            CustomCss = "#progressbar { visibility: hidden; }",
            Accounts =
            [
                new AdminManagedAccountSettings
                {
                    AccountIdentifier = CreateAccountIdentifier(),
                    ShowFavorites = true,
                    ShowMemories = false,
                    ShowArchived = false,
                    ShowVideos = false,
                    Albums = [],
                    ExcludedAlbums = [],
                    People = [],
                    Tags = []
                }
            ]
        };

        var updateResponse = await adminClient.PutAsJsonAsync("/api/admin/settings", updateRequest);
        updateResponse.EnsureSuccessStatusCode();

        var updatedSettings = await updateResponse.Content.ReadFromJsonAsync<AdminSettingsResponseDto>();
        Assert.That(updatedSettings, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(updatedSettings!.General.Interval, Is.EqualTo(90));
            Assert.That(updatedSettings.General.ShowClock, Is.False);
            Assert.That(updatedSettings.General.ShowWeather, Is.False);
            Assert.That(updatedSettings.CustomCss, Is.EqualTo(expectedCustomCss));
            Assert.That(updatedSettings.Accounts[0].ShowFavorites, Is.True);
        });

        var commands = await frameClient.GetFromJsonAsync<List<AdminCommandDto>>("/api/frame-sessions/frame-settings/commands");
        Assert.That(commands, Is.Not.Null);
        Assert.That(commands!, Has.Count.EqualTo(1));
        Assert.That(commands[0].CommandType, Is.EqualTo(FrameAdminCommandType.Refresh));

        var persisted = await adminClient.GetFromJsonAsync<AdminSettingsResponseDto>("/api/admin/settings");
        Assert.That(persisted, Is.Not.Null);
        Assert.That(persisted!.General.Interval, Is.EqualTo(90));
        Assert.That(persisted.Accounts[0].ShowFavorites, Is.True);
        Assert.That(persisted.CustomCss, Is.EqualTo(expectedCustomCss));

        var stylesheetResponse = await _factory.CreateClient().GetAsync("/static/custom.css");
        stylesheetResponse.EnsureSuccessStatusCode();
        Assert.That(await stylesheetResponse.Content.ReadAsStringAsync(), Is.EqualTo(expectedCustomCss));
    }

    [Test]
    public async Task Update_ReturnsBadRequest_WhenWeatherEnabledWithoutApiKey()
    {
        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var updateRequest = new AdminSettingsUpdateRequest
        {
            General = new AdminManagedGeneralSettings
            {
                Interval = 45,
                ShowClock = true,
                ShowWeather = true,
                ShowCalendar = true,
                WeatherApiKey = "",
                ClockFormat = "hh:mm",
                ClockDateFormat = "eee, MMM d",
                PhotoDateFormat = "MM/dd/yyyy",
                ImageLocationFormat = "City,State,Country",
                Layout = "splitview",
                Language = "en",
                ShowProgressBar = true,
                ShowPhotoDate = true,
                ShowImageDesc = true,
                ShowPeopleDesc = true,
                ShowTagsDesc = true,
                ShowAlbumName = true,
                ShowImageLocation = true,
                ShowWeatherDescription = true,
                ImageZoom = true,
                ImagePan = false,
                ImageFill = false,
                PlayAudio = false,
                Style = "none",
                Webcalendars = ["https://calendar.example.com/basic.ics"]
            },
            CustomCss = string.Empty,
            Accounts =
            [
                new AdminManagedAccountSettings
                {
                    AccountIdentifier = CreateAccountIdentifier(),
                    ShowFavorites = false,
                    ShowMemories = false,
                    ShowArchived = false,
                    ShowVideos = false,
                    Albums = [],
                    ExcludedAlbums = [],
                    People = [],
                    Tags = []
                }
            ]
        };

        var response = await adminClient.PutAsJsonAsync("/api/admin/settings", updateRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Update_ReturnsBadRequest_WhenCalendarEnabledWithoutFeeds()
    {
        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var updateRequest = new AdminSettingsUpdateRequest
        {
            General = new AdminManagedGeneralSettings
            {
                Interval = 45,
                ShowClock = true,
                ShowWeather = false,
                ShowCalendar = true,
                ClockFormat = "hh:mm",
                ClockDateFormat = "eee, MMM d",
                PhotoDateFormat = "MM/dd/yyyy",
                ImageLocationFormat = "City,State,Country",
                Layout = "splitview",
                Language = "en",
                ShowProgressBar = true,
                ShowPhotoDate = true,
                ShowImageDesc = true,
                ShowPeopleDesc = true,
                ShowTagsDesc = true,
                ShowAlbumName = true,
                ShowImageLocation = true,
                ShowWeatherDescription = true,
                ImageZoom = true,
                ImagePan = false,
                ImageFill = false,
                PlayAudio = false,
                Style = "none",
                Webcalendars = []
            },
            CustomCss = string.Empty,
            Accounts =
            [
                new AdminManagedAccountSettings
                {
                    AccountIdentifier = CreateAccountIdentifier(),
                    ShowFavorites = false,
                    ShowMemories = false,
                    ShowArchived = false,
                    ShowVideos = false,
                    Albums = [],
                    ExcludedAlbums = [],
                    People = [],
                    Tags = []
                }
            ]
        };

        var response = await adminClient.PutAsJsonAsync("/api/admin/settings", updateRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Update_ReturnsBadRequest_WhenCustomCssContainsImport()
    {
        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var updateRequest = new AdminSettingsUpdateRequest
        {
            General = new AdminManagedGeneralSettings
            {
                Interval = 45,
                ShowClock = true,
                ShowWeather = false,
                ShowCalendar = false,
                ClockFormat = "hh:mm",
                ClockDateFormat = "eee, MMM d",
                PhotoDateFormat = "MM/dd/yyyy",
                ImageLocationFormat = "City,State,Country",
                Layout = "splitview",
                Language = "en",
                ShowProgressBar = true,
                ShowPhotoDate = true,
                ShowImageDesc = true,
                ShowPeopleDesc = true,
                ShowTagsDesc = true,
                ShowAlbumName = true,
                ShowImageLocation = true,
                ShowWeatherDescription = true,
                ImageZoom = true,
                ImagePan = false,
                ImageFill = false,
                PlayAudio = false,
                Style = "none",
                Webcalendars = []
            },
            CustomCss = "@import url('https://example.com/evil.css');",
            Accounts =
            [
                new AdminManagedAccountSettings
                {
                    AccountIdentifier = CreateAccountIdentifier(),
                    ShowFavorites = false,
                    ShowMemories = false,
                    ShowArchived = false,
                    ShowVideos = false,
                    Albums = [],
                    ExcludedAlbums = [],
                    People = [],
                    Tags = []
                }
            ]
        };

        var response = await adminClient.PutAsJsonAsync("/api/admin/settings", updateRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    private static async Task LoginAdminAsync(HttpClient adminClient)
    {
        var loginResponse = await adminClient.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest
        {
            Username = "admin",
            Password = "secret"
        });

        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), await loginResponse.Content.ReadAsStringAsync());
    }

    private static string CreateAccountIdentifier()
    {
        return ServerSettingsFactory.BuildAccountIdentifier(new ServerAccountSettings
        {
            ImmichServerUrl = TestImmichServerUrl,
            ApiKey = TestApiKey
        });
    }
}
