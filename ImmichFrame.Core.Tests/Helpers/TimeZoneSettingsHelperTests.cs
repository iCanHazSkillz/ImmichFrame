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

    [Test]
    public void ResolveCalendarTimeZoneId_ThrowsForUnknownOverride()
    {
        Assert.Throws<TimeZoneNotFoundException>(() =>
            TimeZoneSettingsHelper.ResolveCalendarTimeZoneId("Definitely/Not-A-TimeZone"));
    }
}
