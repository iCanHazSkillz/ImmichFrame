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
    public async Task Get_MigratesLegacyCredentialDerivedAccountIdentifiers()
    {
        var storePath = Path.Combine(_tempAppDataPath, "admin-settings.json");
        var legacyIdentifier = ServerSettingsFactory.BuildLegacyAccountIdentifier(new ServerAccountSettings
        {
            ImmichServerUrl = TestImmichServerUrl,
            ApiKey = TestApiKey
        });

        await File.WriteAllTextAsync(storePath, $$"""
        {
          "General": {
            "Interval": 45,
            "ShowClock": true,
            "ShowWeather": true,
            "ShowCalendar": true,
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
              "AccountIdentifier": "{{legacyIdentifier}}",
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
            Assert.That(response!.Accounts, Has.Count.EqualTo(1));
            Assert.That(response.Accounts[0].AccountIdentifier, Is.EqualTo(CreateAccountIdentifier()));
            Assert.That(response.Accounts[0].AccountIdentifier, Is.Not.EqualTo(legacyIdentifier));
        });

        var persisted = System.Text.Json.JsonSerializer.Deserialize<AdminManagedSettingsDocument>(await File.ReadAllTextAsync(storePath));
        Assert.That(persisted, Is.Not.Null);
        Assert.That(persisted!.Accounts[0].AccountIdentifier, Is.EqualTo(CreateAccountIdentifier()));
    }

    [Test]
    public async Task Update_PreservesDormantStoredAccountOverridesDuringRoundTrip()
    {
        var dormantAccountIdentifier = "DORMANT-ACCOUNT-ID";
        var dormantAlbumId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var dormantPersonId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var storePath = Path.Combine(_tempAppDataPath, "admin-settings.json");

        await File.WriteAllTextAsync(storePath, $$"""
        {
          "General": {
            "Interval": 45,
            "ShowClock": true,
            "ShowWeather": true,
            "ShowCalendar": true,
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
              "AccountIdentifier": "{{CreateAccountIdentifier()}}",
              "ShowFavorites": true,
              "ShowMemories": false,
              "ShowArchived": false,
              "ShowVideos": false,
              "Albums": [],
              "ExcludedAlbums": [],
              "People": [],
              "Tags": []
            },
            {
              "AccountIdentifier": "{{dormantAccountIdentifier}}",
              "ShowFavorites": false,
              "ShowMemories": false,
              "ShowArchived": false,
              "ShowVideos": false,
              "Albums": ["{{dormantAlbumId}}"],
              "ExcludedAlbums": [],
              "People": ["{{dormantPersonId}}"],
              "Tags": ["dormant-tag"]
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
        Assert.That(response!.Accounts, Has.Count.EqualTo(1));
        Assert.That(response.Accounts[0].AccountIdentifier, Is.EqualTo(CreateAccountIdentifier()));

        var updateRequest = new AdminSettingsUpdateRequest
        {
            General = response.General,
            CustomCss = response.CustomCss,
            Accounts = response.Accounts.Select(account => new AdminManagedAccountSettings
            {
                AccountIdentifier = account.AccountIdentifier,
                ShowMemories = account.ShowMemories,
                ShowFavorites = account.ShowFavorites,
                ShowArchived = account.ShowArchived,
                ShowVideos = account.ShowVideos,
                ImagesFromDays = account.ImagesFromDays,
                ImagesFromDate = account.ImagesFromDate,
                ImagesUntilDate = account.ImagesUntilDate,
                Albums = account.Albums.ToList(),
                ExcludedAlbums = account.ExcludedAlbums.ToList(),
                People = account.People.ToList(),
                Tags = account.Tags.ToList(),
                Rating = account.Rating
            }).ToList()
        };
        updateRequest.General.ShowWeather = false;
        updateRequest.General.ShowCalendar = false;

        var updateResponse = await adminClient.PutAsJsonAsync("/api/admin/settings", updateRequest);
        updateResponse.EnsureSuccessStatusCode();

        var roundTripped = await adminClient.GetFromJsonAsync<AdminSettingsResponseDto>("/api/admin/settings");
        Assert.That(roundTripped, Is.Not.Null);
        Assert.That(roundTripped!.Accounts, Has.Count.EqualTo(1));
        Assert.That(roundTripped.Accounts[0].AccountIdentifier, Is.EqualTo(CreateAccountIdentifier()));

        var persisted = System.Text.Json.JsonSerializer.Deserialize<AdminManagedSettingsDocument>(await File.ReadAllTextAsync(storePath));
        Assert.That(persisted, Is.Not.Null);

        var dormantPersistedAccount = persisted!.Accounts.SingleOrDefault(account =>
            string.Equals(account.AccountIdentifier, dormantAccountIdentifier, StringComparison.Ordinal));

        Assert.That(dormantPersistedAccount, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dormantPersistedAccount!.Albums, Is.EqualTo(new[] { dormantAlbumId }));
            Assert.That(dormantPersistedAccount.People, Is.EqualTo(new[] { dormantPersonId }));
            Assert.That(dormantPersistedAccount.Tags, Is.EqualTo(new[] { "dormant-tag" }));
        });
    }

    [Test]
    public async Task Update_PersistsSettingsQueuesRefreshAndServesCustomCss()
    {
        const string expectedCustomCss = "#progressbar { visibility: hidden; }";
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
    public async Task Update_PreservesSafeMultilineCustomCss()
    {
        var customCss = """
            #overlayback,
            #overlayInfo,
            #overlaypause,
            #overlaynext,
            #overlayback *,
            #overlayInfo *,
            #overlaypause *,
            #overlaynext * {
              display: none;
              visibility: hidden;
              pointer-events: none;
            }
            #progressbar{
              height: 7px;
              opacity: 0.95;
            }
            #imageinfo {
              left: 1rem;
              right: auto;
              text-shadow:
                0 0 2px rgba(0, 0, 0, 0.95),
                0 1px 3px rgba(0, 0, 0, 0.9),
                0 2px 6px rgba(0, 0, 0, 0.75);
            }

            /* Make the icons stand out too */
            #imageinfo svg {
              filter: drop-shadow(0 1px 2px rgba(0, 0, 0, 0.9))
                      drop-shadow(0 2px 4px rgba(0, 0, 0, 0.7));
            }
            """;

        var expectedCustomCss = customCss
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();

        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var updateResponse = await adminClient.PutAsJsonAsync("/api/admin/settings", CreateValidUpdateRequest(expectedCustomCss));
        updateResponse.EnsureSuccessStatusCode();

        var updatedSettings = await updateResponse.Content.ReadFromJsonAsync<AdminSettingsResponseDto>();
        Assert.That(updatedSettings, Is.Not.Null);
        Assert.That(updatedSettings!.CustomCss, Is.EqualTo(expectedCustomCss));

        var persistedSettings = await adminClient.GetFromJsonAsync<AdminSettingsResponseDto>("/api/admin/settings");
        Assert.That(persistedSettings, Is.Not.Null);
        Assert.That(persistedSettings!.CustomCss, Is.EqualTo(expectedCustomCss));

        var stylesheetResponse = await _factory.CreateClient().GetAsync("/static/custom.css");
        stylesheetResponse.EnsureSuccessStatusCode();
        Assert.That(await stylesheetResponse.Content.ReadAsStringAsync(), Is.EqualTo(expectedCustomCss));
    }

    [Test]
    public async Task Get_AndUpdate_DoNotExposeOrPersistSecretBootstrapSettings()
    {
        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var initialJson = await adminClient.GetStringAsync("/api/admin/settings");
        Assert.Multiple(() =>
        {
            Assert.That(initialJson, Does.Not.Contain("weatherApiKey"));
            Assert.That(initialJson, Does.Not.Contain("webhook"));
        });

        var updateRequest = new AdminSettingsUpdateRequest
        {
            General = new AdminManagedGeneralSettings
            {
                Interval = 45,
                ShowClock = true,
                ShowWeather = true,
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
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(responseJson, Does.Not.Contain("weatherApiKey"));
            Assert.That(responseJson, Does.Not.Contain("webhook"));
        });

        var persistedJson = await File.ReadAllTextAsync(Path.Combine(_tempAppDataPath, "admin-settings.json"));
        Assert.Multiple(() =>
        {
            Assert.That(persistedJson, Does.Not.Contain("WeatherApiKey"));
            Assert.That(persistedJson, Does.Not.Contain("Webhook"));
        });
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

        var updateRequest = CreateValidUpdateRequest("@import url('https://example.com/evil.css');");

        var response = await adminClient.PutAsJsonAsync("/api/admin/settings", updateRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Update_ReturnsBadRequest_WhenCustomCssContainsExpression()
    {
        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var response = await adminClient.PutAsJsonAsync(
            "/api/admin/settings",
            CreateValidUpdateRequest("#imageinfo { width: expression(alert('x')); }"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Update_ReturnsBadRequest_WhenCustomCssContainsDataUrl()
    {
        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var response = await adminClient.PutAsJsonAsync(
            "/api/admin/settings",
            CreateValidUpdateRequest("#imageinfo { background-image: url(data:text/plain;base64,SGVsbG8=); }"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Update_ReturnsBadRequest_WhenCustomCssIsInvalid_AndKeepsPreviousStylesheet()
    {
        const string validCustomCss = "#progressbar { visibility: hidden; }";
        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var validResponse = await adminClient.PutAsJsonAsync("/api/admin/settings", CreateValidUpdateRequest(validCustomCss));
        validResponse.EnsureSuccessStatusCode();

        var invalidResponse = await adminClient.PutAsJsonAsync(
            "/api/admin/settings",
            CreateValidUpdateRequest("#imageinfo { color: red;"));

        Assert.That(invalidResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var persistedSettings = await adminClient.GetFromJsonAsync<AdminSettingsResponseDto>("/api/admin/settings");
        Assert.That(persistedSettings, Is.Not.Null);
        Assert.That(persistedSettings!.CustomCss, Is.EqualTo(validCustomCss));

        var stylesheetResponse = await _factory.CreateClient().GetAsync("/static/custom.css");
        stylesheetResponse.EnsureSuccessStatusCode();
        Assert.That(await stylesheetResponse.Content.ReadAsStringAsync(), Is.EqualTo(validCustomCss));
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

    private static AdminSettingsUpdateRequest CreateValidUpdateRequest(string customCss)
    {
        return new AdminSettingsUpdateRequest
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
            CustomCss = customCss,
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
    }
}
