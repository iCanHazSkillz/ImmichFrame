using ImmichFrame.Core.Helpers;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Helpers;

[TestFixture]
public class TimeZoneSettingsHelperTests
{
    [Test]
    public void NormalizeTimeZoneId_MapsWindowsIdentifiersToCanonicalIana()
    {
        var normalized = TimeZoneSettingsHelper.NormalizeTimeZoneId("Eastern Standard Time");

        Assert.That(normalized, Is.EqualTo("America/New_York"));
    }

    [TestCase("America/Edmonton")]
    [TestCase("America/Denver")]
    [TestCase("America/Toronto")]
    [TestCase("Europe/Berlin")]
    public void NormalizeTimeZoneId_KeepsValidIanaIdentifiers(string ianaId)
    {
        // Regression: America/Edmonton used to be rewritten to America/Denver via the Windows
        // "Mountain Standard Time" zone, so calendars followed Colorado's DST rules instead of Alberta's.
        Assert.That(TimeZoneSettingsHelper.NormalizeTimeZoneId(ianaId), Is.EqualTo(ianaId));
    }

    [Test]
    public void ResolveCalendarTimeZoneId_KeepsConfiguredIanaOverride()
    {
        Assert.That(TimeZoneSettingsHelper.ResolveCalendarTimeZoneId("America/Edmonton"), Is.EqualTo("America/Edmonton"));
    }

    [Test]
    public void ResolveCalendarTimeZoneId_ThrowsForUnknownOverride()
    {
        Assert.Throws<TimeZoneNotFoundException>(() =>
            TimeZoneSettingsHelper.ResolveCalendarTimeZoneId("Definitely/Not-A-TimeZone"));
    }
}
