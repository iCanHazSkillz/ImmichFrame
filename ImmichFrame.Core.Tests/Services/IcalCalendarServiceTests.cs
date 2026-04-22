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
        var localNow = new DateTime(todayInTimeZone.Year, todayInTimeZone.Month, todayInTimeZone.Day, 8, 0, 0, DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, calendarTimeZone);
        var utcEnd = utcStart.AddHours(1);
        var utcNow = TimeZoneInfo.ConvertTimeToUtc(localNow, calendarTimeZone);

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
            httpClientFactory.Object,
            () => new DateTimeOffset(utcNow, TimeSpan.Zero));

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
        var localNow = new DateTime(todayInTimeZone.Year, todayInTimeZone.Month, todayInTimeZone.Day, 8, 0, 0, DateTimeKind.Unspecified);
        var initialOccurrenceStart = localStart.AddDays(-1);
        var initialOccurrenceEnd = initialOccurrenceStart.AddHours(1);
        var utcNow = TimeZoneInfo.ConvertTimeToUtc(localNow, calendarTimeZone);

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
            httpClientFactory.Object,
            () => new DateTimeOffset(utcNow, TimeSpan.Zero));

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

    [Test]
    public async Task GetAppointments_DefaultLookaheadExcludesTomorrow()
    {
        var now = new DateTimeOffset(2026, 4, 21, 8, 0, 0, TimeSpan.Zero);
        var ics = """
                  BEGIN:VCALENDAR
                  VERSION:2.0
                  PRODID:-//ImmichFrame.Tests//EN
                  BEGIN:VEVENT
                  UID:tomorrow-event
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260422T090000Z
                  DTEND:20260422T100000Z
                  SUMMARY:Tomorrow event
                  END:VEVENT
                  END:VCALENDAR
                  """;

        var service = CreateCalendarService(ics, now);

        var appointments = await service.GetAppointments();

        Assert.That(appointments, Is.Empty);
    }

    [Test]
    public async Task GetAppointments_LookaheadIncludesTomorrow()
    {
        var now = new DateTimeOffset(2026, 4, 21, 8, 0, 0, TimeSpan.Zero);
        var ics = """
                  BEGIN:VCALENDAR
                  VERSION:2.0
                  PRODID:-//ImmichFrame.Tests//EN
                  BEGIN:VEVENT
                  UID:tomorrow-event
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260422T090000Z
                  DTEND:20260422T100000Z
                  SUMMARY:Tomorrow event
                  END:VEVENT
                  END:VCALENDAR
                  """;

        var service = CreateCalendarService(ics, now, lookaheadDays: 1);

        var appointments = await service.GetAppointments();

        Assert.That(appointments.Select(appointment => appointment.Summary), Is.EqualTo(new[] { "Tomorrow event" }));
    }

    [Test]
    public async Task GetAppointments_ExcludesPastEventsAndKeepsCurrentAndFutureEvents()
    {
        var now = new DateTimeOffset(2026, 4, 21, 8, 1, 0, TimeSpan.Zero);
        var ics = """
                  BEGIN:VCALENDAR
                  VERSION:2.0
                  PRODID:-//ImmichFrame.Tests//EN
                  BEGIN:VEVENT
                  UID:past-event
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T060000Z
                  DTEND:20260421T070000Z
                  SUMMARY:Past event
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:current-event
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T080000Z
                  DTEND:20260421T090000Z
                  SUMMARY:Current event
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:future-event
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T100000Z
                  DTEND:20260421T110000Z
                  SUMMARY:Future event
                  END:VEVENT
                  END:VCALENDAR
                  """;

        var service = CreateCalendarService(ics, now);

        var appointments = await service.GetAppointments();

        Assert.That(
            appointments.Select(appointment => appointment.Summary),
            Is.EqualTo(new[] { "Current event", "Future event" }));
    }

    [Test]
    public async Task GetAppointments_ExpandsRecurringEventsAcrossLookaheadRange()
    {
        var now = new DateTimeOffset(2026, 4, 21, 8, 0, 0, TimeSpan.Zero);
        var ics = """
                  BEGIN:VCALENDAR
                  VERSION:2.0
                  PRODID:-//ImmichFrame.Tests//EN
                  BEGIN:VEVENT
                  UID:daily-event
                  DTSTAMP:20260420T000000Z
                  DTSTART:20260420T090000Z
                  DTEND:20260420T100000Z
                  RRULE:FREQ=DAILY;COUNT=4
                  SUMMARY:Daily event
                  END:VEVENT
                  END:VCALENDAR
                  """;

        var service = CreateCalendarService(ics, now, lookaheadDays: 2, maxEvents: 10);

        var appointments = await service.GetAppointments();

        Assert.That(appointments.Select(appointment => appointment.StartTime.Day), Is.EqualTo(new[] { 21, 22, 23 }));
    }

    [Test]
    public async Task GetAppointments_SortsEventsChronologicallyAndAppliesMaxEvents()
    {
        var now = new DateTimeOffset(2026, 4, 21, 8, 0, 0, TimeSpan.Zero);
        var ics = """
                  BEGIN:VCALENDAR
                  VERSION:2.0
                  PRODID:-//ImmichFrame.Tests//EN
                  BEGIN:VEVENT
                  UID:late-event
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T120000Z
                  DTEND:20260421T130000Z
                  SUMMARY:Late event
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:first-event
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T090000Z
                  DTEND:20260421T100000Z
                  SUMMARY:First event
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:second-event
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T100000Z
                  DTEND:20260421T110000Z
                  SUMMARY:Second event
                  END:VEVENT
                  END:VCALENDAR
                  """;

        var service = CreateCalendarService(ics, now, maxEvents: 2);

        var appointments = await service.GetAppointments();

        Assert.That(
            appointments.Select(appointment => appointment.Summary),
            Is.EqualTo(new[] { "First event", "Second event" }));
    }

    [Test]
    public async Task GetAppointments_SortsDayGroupsDescendingAndKeepsEventsWithinDayAscending()
    {
        var now = new DateTimeOffset(2026, 4, 21, 8, 0, 0, TimeSpan.Zero);
        var ics = """
                  BEGIN:VCALENDAR
                  VERSION:2.0
                  PRODID:-//ImmichFrame.Tests//EN
                  BEGIN:VEVENT
                  UID:first-event
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T090000Z
                  DTEND:20260421T100000Z
                  SUMMARY:First event
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:tomorrow-late-event
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260422T120000Z
                  DTEND:20260422T130000Z
                  SUMMARY:Tomorrow late event
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:tomorrow-first-event
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260422T090000Z
                  DTEND:20260422T100000Z
                  SUMMARY:Tomorrow first event
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:second-event
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T100000Z
                  DTEND:20260421T110000Z
                  SUMMARY:Second event
                  END:VEVENT
                  END:VCALENDAR
                  """;

        var service = CreateCalendarService(ics, now, lookaheadDays: 1, maxEvents: 4, sortDirection: "descending");

        var appointments = await service.GetAppointments();

        Assert.That(
            appointments.Select(appointment => appointment.Summary),
            Is.EqualTo(new[]
            {
                "Tomorrow first event",
                "Tomorrow late event",
                "First event",
                "Second event"
            }));
    }

    [Test]
    public async Task GetAppointments_WhenFirstCachedEventPasses_PromotesNextEventOnRefresh()
    {
        var currentNow = new DateTimeOffset(2026, 4, 21, 8, 0, 0, TimeSpan.Zero);
        var ics = """
                  BEGIN:VCALENDAR
                  VERSION:2.0
                  PRODID:-//ImmichFrame.Tests//EN
                  BEGIN:VEVENT
                  UID:event-01
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T080000Z
                  DTEND:20260421T083000Z
                  SUMMARY:Event 01
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:event-02
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T090000Z
                  DTEND:20260421T093000Z
                  SUMMARY:Event 02
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:event-03
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T100000Z
                  DTEND:20260421T103000Z
                  SUMMARY:Event 03
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:event-04
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T110000Z
                  DTEND:20260421T113000Z
                  SUMMARY:Event 04
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:event-05
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T120000Z
                  DTEND:20260421T123000Z
                  SUMMARY:Event 05
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:event-06
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T130000Z
                  DTEND:20260421T133000Z
                  SUMMARY:Event 06
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:event-07
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T140000Z
                  DTEND:20260421T143000Z
                  SUMMARY:Event 07
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:event-08
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T150000Z
                  DTEND:20260421T153000Z
                  SUMMARY:Event 08
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:event-09
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260421T160000Z
                  DTEND:20260421T163000Z
                  SUMMARY:Event 09
                  END:VEVENT
                  BEGIN:VEVENT
                  UID:event-10
                  DTSTAMP:20260421T000000Z
                  DTSTART:20260422T090000Z
                  DTEND:20260422T093000Z
                  SUMMARY:Event 10
                  END:VEVENT
                  END:VCALENDAR
                  """;

        var service = CreateCalendarService(ics, () => currentNow, lookaheadDays: 3, maxEvents: 9);

        var initialAppointments = await service.GetAppointments();
        currentNow = new DateTimeOffset(2026, 4, 21, 8, 31, 0, TimeSpan.Zero);
        var refreshedAppointments = await service.GetAppointments();

        Assert.Multiple(() =>
        {
            Assert.That(initialAppointments.Select(appointment => appointment.Summary), Is.EqualTo(new[]
            {
                "Event 01",
                "Event 02",
                "Event 03",
                "Event 04",
                "Event 05",
                "Event 06",
                "Event 07",
                "Event 08",
                "Event 09"
            }));
            Assert.That(refreshedAppointments.Select(appointment => appointment.Summary), Is.EqualTo(new[]
            {
                "Event 02",
                "Event 03",
                "Event 04",
                "Event 05",
                "Event 06",
                "Event 07",
                "Event 08",
                "Event 09",
                "Event 10"
            }));
        });
    }

    private static IcalCalendarService CreateCalendarService(
        string ics,
        DateTimeOffset utcNow,
        int lookaheadDays = 0,
        int maxEvents = 5,
        string calendarTimeZone = "UTC",
        string sortDirection = "ascending")
    {
        return CreateCalendarService(ics, () => utcNow, lookaheadDays, maxEvents, calendarTimeZone, sortDirection);
    }

    private static IcalCalendarService CreateCalendarService(
        string ics,
        Func<DateTimeOffset> utcNowProvider,
        int lookaheadDays = 0,
        int maxEvents = 5,
        string calendarTimeZone = "UTC",
        string sortDirection = "ascending")
    {
        var handler = new CaptureHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ics)
            }));

        var httpClient = new HttpClient(handler);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var generalSettings = new Mock<IGeneralSettings>();
        generalSettings.SetupGet(x => x.Webcalendars).Returns(["https://calendar.example.com/basic.ics"]);
        generalSettings.SetupGet(x => x.CalendarTimeZone).Returns(calendarTimeZone);
        generalSettings.SetupGet(x => x.CalendarLookaheadDays).Returns(lookaheadDays);
        generalSettings.SetupGet(x => x.CalendarMaxEvents).Returns(maxEvents);
        generalSettings.SetupGet(x => x.CalendarSortDirection).Returns(sortDirection);

        var serverSettings = new Mock<IServerSettings>();
        serverSettings.SetupGet(x => x.GeneralSettings).Returns(generalSettings.Object);

        var settingsProvider = new Mock<ISettingsSnapshotProvider>();
        settingsProvider.Setup(x => x.GetCurrentSnapshot()).Returns(new SettingsSnapshot(1, serverSettings.Object));

        return new IcalCalendarService(
            settingsProvider.Object,
            NullLogger<IcalCalendarService>.Instance,
            httpClientFactory.Object,
            utcNowProvider);
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
