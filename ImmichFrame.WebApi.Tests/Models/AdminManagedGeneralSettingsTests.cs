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
            ShowPhotoTimeAgo = true
        };
        var server = new GeneralSettings();

        managed.ApplyTo(server);
        var roundTripped = AdminManagedGeneralSettings.FromGeneralSettings(server);

        Assert.Multiple(() =>
        {
            Assert.That(server.ShowPeopleAge, Is.True);
            Assert.That(server.ShowPhotoTimeAgo, Is.True);
            Assert.That(roundTripped.ShowPeopleAge, Is.True);
            Assert.That(roundTripped.ShowPhotoTimeAgo, Is.True);
        });
    }
}
