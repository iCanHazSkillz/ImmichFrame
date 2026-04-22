using ImmichFrame.WebApi.Models;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.Models;

[TestFixture]
public class AdminManagedGeneralSettingsTests
{
    [Test]
    public void Normalize_CollapsesDuplicatedPercentEncodingInCalendarUrls()
    {
        var settings = new AdminManagedGeneralSettings
        {
            Webcalendars =
            [
                "  https://calendar.google.com/calendar/ical/martel.s.g%%40gmail.com/public/basic.ics  ",
                "",
                "   "
            ]
        };

        settings.Normalize();

        Assert.That(settings.Webcalendars, Is.EqualTo(new[]
        {
            "https://calendar.google.com/calendar/ical/martel.s.g%40gmail.com/public/basic.ics"
        }));
    }

    [Test]
    public void Normalize_DisablesPeopleAgeWhenPeopleDescriptionsAreHidden()
    {
        var settings = new AdminManagedGeneralSettings
        {
            ShowPeopleDesc = false,
            ShowPeopleAge = true
        };

        settings.Normalize();

        Assert.That(settings.ShowPeopleAge, Is.False);
    }

    [Test]
    public void DocumentNormalize_AlsoNormalizesGeneralSettings()
    {
        var document = new AdminManagedSettingsDocument
        {
            General = new AdminManagedGeneralSettings
            {
                ShowPeopleDesc = false,
                ShowPeopleAge = true,
                Webcalendars =
                [
                    "  https://calendar.google.com/calendar/ical/martel.s.g%%40gmail.com/public/basic.ics  "
                ]
            }
        };

        document.Normalize();

        Assert.Multiple(() =>
        {
            Assert.That(document.General.ShowPeopleAge, Is.False);
            Assert.That(document.General.Webcalendars, Is.EqualTo(new[]
            {
                "https://calendar.google.com/calendar/ical/martel.s.g%40gmail.com/public/basic.ics"
            }));
        });
    }

    [Test]
    public void ApplyTo_AndFromGeneralSettings_RoundTripPhotoTimeAgoAndPeopleAge()
    {
        var managed = new AdminManagedGeneralSettings
        {
            ShowPeopleDesc = true,
            ShowPeopleAge = true,
            ShowPhotoTimeAgo = true,
            CalendarLookaheadDays = 3,
            CalendarMaxEvents = 8,
            CalendarSortDirection = "descending"
        };
        var server = new GeneralSettings();

        managed.ApplyTo(server);
        var roundTripped = AdminManagedGeneralSettings.FromGeneralSettings(server);

        Assert.Multiple(() =>
        {
            Assert.That(server.ShowPeopleAge, Is.True);
            Assert.That(server.ShowPhotoTimeAgo, Is.True);
            Assert.That(server.CalendarLookaheadDays, Is.EqualTo(3));
            Assert.That(server.CalendarMaxEvents, Is.EqualTo(8));
            Assert.That(server.CalendarSortDirection, Is.EqualTo("descending"));
            Assert.That(roundTripped.ShowPeopleAge, Is.True);
            Assert.That(roundTripped.ShowPhotoTimeAgo, Is.True);
            Assert.That(roundTripped.CalendarLookaheadDays, Is.EqualTo(3));
            Assert.That(roundTripped.CalendarMaxEvents, Is.EqualTo(8));
            Assert.That(roundTripped.CalendarSortDirection, Is.EqualTo("descending"));
        });
    }

    [Test]
    public void Normalize_ClampsCalendarLookaheadAndMaxEvents()
    {
        var tooHigh = new AdminManagedGeneralSettings
        {
            CalendarLookaheadDays = 99,
            CalendarMaxEvents = 99
        };
        var tooLow = new AdminManagedGeneralSettings
        {
            CalendarLookaheadDays = -1,
            CalendarMaxEvents = 0
        };

        tooHigh.Normalize();
        tooLow.Normalize();

        Assert.Multiple(() =>
        {
            Assert.That(tooHigh.CalendarLookaheadDays, Is.EqualTo(7));
            Assert.That(tooHigh.CalendarMaxEvents, Is.EqualTo(10));
            Assert.That(tooLow.CalendarLookaheadDays, Is.EqualTo(0));
            Assert.That(tooLow.CalendarMaxEvents, Is.EqualTo(1));
        });
    }

    [Test]
    public void Normalize_DefaultsUnknownCalendarSortDirectionToAscending()
    {
        var settings = new AdminManagedGeneralSettings
        {
            CalendarSortDirection = "sideways"
        };

        settings.Normalize();

        Assert.That(settings.CalendarSortDirection, Is.EqualTo("ascending"));
    }
}
