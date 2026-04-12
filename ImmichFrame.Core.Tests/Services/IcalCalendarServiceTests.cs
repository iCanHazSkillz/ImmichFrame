using System.Net;
using System.Net.Http;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Services;

[TestFixture]
public class IcalCalendarServiceTests
{
    [Test]
    public async Task GetAppointments_NormalizesDuplicatedPercentEncodingInCalendarUrls()
    {
        var requestedUris = new List<Uri>();
        using var handler = new CaptureHttpMessageHandler((request, _) =>
        {
            requestedUris.Add(request.RequestUri!);

            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            var ics = $"""
                       BEGIN:VCALENDAR
                       VERSION:2.0
                       PRODID:-//ImmichFrame.Tests//EN
                       BEGIN:VEVENT
                       UID:test-event
                       DTSTAMP:{today:yyyyMMdd}T000000Z
                       DTSTART;VALUE=DATE:{today:yyyyMMdd}
                       DTEND;VALUE=DATE:{tomorrow:yyyyMMdd}
                       SUMMARY:Test event
                       END:VEVENT
                       END:VCALENDAR
                       """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ics)
            });
        });

        using var httpClient = new HttpClient(handler);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var generalSettings = new Mock<IGeneralSettings>();
        generalSettings.SetupGet(x => x.Webcalendars).Returns(
        [
            "https://calendar.google.com/calendar/ical/martel.s.g%%40gmail.com/public/basic.ics"
        ]);

        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.GeneralSettings).Returns(generalSettings.Object);

        var settingsProvider = new Mock<ISettingsSnapshotProvider>();
        settingsProvider.Setup(x => x.GetCurrentSnapshot()).Returns(new SettingsSnapshot(1, serverSettings.Object));

        var service = new IcalCalendarService(
            settingsProvider.Object,
            NullLogger<IcalCalendarService>.Instance,
            httpClientFactory.Object);

        await service.GetAppointments();

        Assert.That(requestedUris, Has.Count.EqualTo(1));
        Assert.That(
            requestedUris[0].AbsoluteUri,
            Is.EqualTo("https://calendar.google.com/calendar/ical/martel.s.g%40gmail.com/public/basic.ics"));
    }

    [Test]
    public async Task GetAppointments_ConvertsReturnedAppointmentTimesToConfiguredCalendarTimeZone()
    {
        var calendarTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Edmonton");
        var todayInTimeZone = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, calendarTimeZone);
        var localStart = new DateTime(todayInTimeZone.Year, todayInTimeZone.Month, todayInTimeZone.Day, 9, 0, 0, DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, calendarTimeZone);
        var utcEnd = utcStart.AddHours(1);

        using var handler = new CaptureHttpMessageHandler((_, _) =>
        {
            var ics = $"""
                       BEGIN:VCALENDAR
                       VERSION:2.0
                       PRODID:-//ImmichFrame.Tests//EN
                       BEGIN:VEVENT
                       UID:test-tz-event
                       DTSTAMP:{utcStart:yyyyMMdd}T000000Z
                       DTSTART:{utcStart:yyyyMMddTHHmmssZ}
                       DTEND:{utcEnd:yyyyMMddTHHmmssZ}
                       SUMMARY:Timezone test
                       END:VEVENT
                       END:VCALENDAR
                       """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ics)
            });
        });

        using var httpClient = new HttpClient(handler);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var generalSettings = new Mock<IGeneralSettings>();
        generalSettings.SetupGet(x => x.Webcalendars).Returns(["https://calendar.example.com/basic.ics"]);
        generalSettings.SetupGet(x => x.CalendarTimeZone).Returns("America/Edmonton");

        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.GeneralSettings).Returns(generalSettings.Object);

        var settingsProvider = new Mock<ISettingsSnapshotProvider>();
        settingsProvider.Setup(x => x.GetCurrentSnapshot()).Returns(new SettingsSnapshot(1, serverSettings.Object));

        var service = new IcalCalendarService(
            settingsProvider.Object,
            NullLogger<IcalCalendarService>.Instance,
            httpClientFactory.Object);

        var appointments = await service.GetAppointments();

        Assert.That(appointments, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(appointments[0].StartTime.Hour, Is.EqualTo(9));
            Assert.That(appointments[0].EndTime.Hour, Is.EqualTo(10));
            Assert.That(appointments[0].StartTime.Offset, Is.EqualTo(TimeZoneInfo.ConvertTime(new DateTimeOffset(utcStart, TimeSpan.Zero), calendarTimeZone).Offset));
        });
    }

    [Test]
    public async Task GetAppointments_DoesNotIncludeUtcEventFromPreviousLocalDay()
    {
        var calendarTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Edmonton");
        var todayInTimeZone = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, calendarTimeZone);
        var previousLocalStart = new DateTime(
            todayInTimeZone.Year,
            todayInTimeZone.Month,
            todayInTimeZone.Day,
            19,
            30,
            0,
            DateTimeKind.Unspecified).AddDays(-1);
        var previousLocalEnd = previousLocalStart.AddHours(1);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(previousLocalStart, calendarTimeZone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(previousLocalEnd, calendarTimeZone);

        using var handler = new CaptureHttpMessageHandler((_, _) =>
        {
            var ics = $"""
                       BEGIN:VCALENDAR
                       VERSION:2.0
                       PRODID:-//ImmichFrame.Tests//EN
                       BEGIN:VEVENT
                       UID:test-previous-local-day
                       DTSTAMP:{utcStart:yyyyMMdd}T000000Z
                       DTSTART:{utcStart:yyyyMMddTHHmmssZ}
                       DTEND:{utcEnd:yyyyMMddTHHmmssZ}
                       SUMMARY:Previous local day event
                       END:VEVENT
                       END:VCALENDAR
                       """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ics)
            });
        });

        using var httpClient = new HttpClient(handler);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var generalSettings = new Mock<IGeneralSettings>();
        generalSettings.SetupGet(x => x.Webcalendars).Returns(["https://calendar.example.com/basic.ics"]);
        generalSettings.SetupGet(x => x.CalendarTimeZone).Returns("America/Edmonton");

        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.GeneralSettings).Returns(generalSettings.Object);

        var settingsProvider = new Mock<ISettingsSnapshotProvider>();
        settingsProvider.Setup(x => x.GetCurrentSnapshot()).Returns(new SettingsSnapshot(1, serverSettings.Object));

        var service = new IcalCalendarService(
            settingsProvider.Object,
            NullLogger<IcalCalendarService>.Instance,
            httpClientFactory.Object);

        var appointments = await service.GetAppointments();

        Assert.That(appointments, Is.Empty);
    }

    [Test]
    public async Task GetAppointments_UsesOccurrenceDateForRecurringEvents()
    {
        var calendarTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Edmonton");
        var todayInTimeZone = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, calendarTimeZone);
        var localStart = new DateTime(todayInTimeZone.Year, todayInTimeZone.Month, todayInTimeZone.Day, 9, 0, 0, DateTimeKind.Unspecified);
        var initialOccurrenceStart = localStart.AddDays(-1);
        var initialOccurrenceEnd = initialOccurrenceStart.AddHours(1);

        using var handler = new CaptureHttpMessageHandler((_, _) =>
        {
            var ics = $"""
                       BEGIN:VCALENDAR
                       VERSION:2.0
                       PRODID:-//ImmichFrame.Tests//EN
                       BEGIN:VEVENT
                       UID:test-recurring-event
                       DTSTAMP:{todayInTimeZone.UtcDateTime:yyyyMMdd}T000000Z
                       DTSTART;TZID=America/Edmonton:{initialOccurrenceStart:yyyyMMddTHHmmss}
                       DTEND;TZID=America/Edmonton:{initialOccurrenceEnd:yyyyMMddTHHmmss}
                       RRULE:FREQ=DAILY;COUNT=2
                       SUMMARY:Recurring event
                       END:VEVENT
                       END:VCALENDAR
                       """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ics)
            });
        });

        using var httpClient = new HttpClient(handler);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var generalSettings = new Mock<IGeneralSettings>();
        generalSettings.SetupGet(x => x.Webcalendars).Returns(["https://calendar.example.com/basic.ics"]);
        generalSettings.SetupGet(x => x.CalendarTimeZone).Returns("America/Edmonton");

        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.GeneralSettings).Returns(generalSettings.Object);

        var settingsProvider = new Mock<ISettingsSnapshotProvider>();
        settingsProvider.Setup(x => x.GetCurrentSnapshot()).Returns(new SettingsSnapshot(1, serverSettings.Object));

        var service = new IcalCalendarService(
            settingsProvider.Object,
            NullLogger<IcalCalendarService>.Instance,
            httpClientFactory.Object);

        var appointments = await service.GetAppointments();

        Assert.That(appointments, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(appointments[0].Summary, Is.EqualTo("Recurring event"));
            Assert.That(appointments[0].StartTime.Hour, Is.EqualTo(9));
            Assert.That(appointments[0].StartTime.Date, Is.EqualTo(localStart.Date));
            Assert.That(appointments[0].EndTime.Hour, Is.EqualTo(10));
        });
    }

    private sealed class CaptureHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}
