using ImmichFrame.WebApi.Models;
using ImmichFrame.WebApi.Services;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.Services;

[TestFixture]
public class EnvSettingsSyncTests
{
    private string _tempPath = null!;

    [SetUp]
    public void Setup()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }
    }

    [Test]
    public void Save_PreservesCommentsAndUpdatesOrAppendsSyncedKeysOnly()
    {
        var envPath = Path.Combine(_tempPath, ".env");
        File.WriteAllText(envPath, """
        # Existing comments stay put
        ImmichServerUrl=http://photos.example.com
        Interval=10
        # Albums=00000000-0000-0000-0000-000000000000
        WidgetStackOrder=clock,weather
        """);

        var albumId = Guid.NewGuid();
        var settings = new AdminManagedSettingsDocument
        {
            General = new AdminManagedGeneralSettings
            {
                Interval = 90,
                TransitionDuration = 2,
                Language = "en",
                Layout = "single",
                Style = "none",
                CalendarSortDirection = "ascending",
                WeatherLatLong = "53.710072,-113.213310",
                Webcalendars = ["https://calendar.example.com/basic.ics"]
            },
            Accounts =
            [
                new AdminManagedAccountSettings
                {
                    AccountIdentifier = "account-1",
                    Albums = [albumId]
                }
            ]
        };

        var sync = new EnvSettingsSync(new EnvSettingsSyncOptions { EnvFilePath = envPath });

        sync.Save(settings, "weather-secret");

        var updated = File.ReadAllText(envPath);
        Assert.Multiple(() =>
        {
            Assert.That(updated, Does.Contain("# Existing comments stay put"));
            Assert.That(updated, Does.Contain("ImmichServerUrl=http://photos.example.com"));
            Assert.That(updated, Does.Contain("Interval=90"));
            Assert.That(updated, Does.Contain($"Albums={albumId}"));
            Assert.That(updated, Does.Contain("Webcalendars=https://calendar.example.com/basic.ics"));
            Assert.That(updated, Does.Contain("WeatherApiKey=weather-secret"));
            Assert.That(updated, Does.Contain("WidgetStackOrder=clock,weather"));
            Assert.That(updated, Does.Not.Contain("AuthenticationSecret="));
        });
    }

    [Test]
    public void ImportInto_UpdatesOnlyPresentSyncedValues()
    {
        var envPath = Path.Combine(_tempPath, ".env");
        var albumId = Guid.NewGuid();
        File.WriteAllText(envPath, $"""
        # Commented settings are ignored
        # Interval=10
        Interval=75
        ShowFavorites=true
        ImagesFromDays=
        Albums={albumId}
        WeatherApiKey=from-env
        ClockPosition=hidden
        """);

        var settings = new AdminManagedSettingsDocument
        {
            General = new AdminManagedGeneralSettings
            {
                Interval = 45,
                ClockPosition = "bottom-left"
            },
            Accounts =
            [
                new AdminManagedAccountSettings
                {
                    AccountIdentifier = "account-1",
                    ShowFavorites = false,
                    ImagesFromDays = 365
                }
            ]
        };

        var sync = new EnvSettingsSync(new EnvSettingsSyncOptions { EnvFilePath = envPath });

        var result = sync.ImportInto(settings);

        Assert.Multiple(() =>
        {
            Assert.That(settings.General.Interval, Is.EqualTo(75));
            Assert.That(settings.General.ClockPosition, Is.EqualTo("bottom-left"));
            Assert.That(settings.Accounts[0].ShowFavorites, Is.True);
            Assert.That(settings.Accounts[0].ImagesFromDays, Is.Null);
            Assert.That(settings.Accounts[0].Albums, Is.EqualTo(new[] { albumId }));
            Assert.That(result.WeatherApiKeyChanged, Is.True);
            Assert.That(result.WeatherApiKey, Is.EqualTo("from-env"));
        });
    }
}
