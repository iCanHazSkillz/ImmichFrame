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

    private sealed class CaptureHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}
