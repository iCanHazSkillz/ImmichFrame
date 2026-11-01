using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic.Pool;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic.Pool;

// Exercises the real generated client against canned Immich v3.3 /memories responses, so the
// JSON deserialization path (which is what failed in production) is covered, not just the mocks.
[TestFixture]
public class MemoryAssetsPoolImmichResponseTests
{
    private static string Memory(string type, string data) => $$"""
        {
          "id": "{{Guid.NewGuid()}}",
          "ownerId": "{{Guid.NewGuid()}}",
          "type": "{{type}}",
          "data": {{data}},
          "isSaved": false,
          "assets": [],
          "createdAt": "2026-10-31T00:00:00.000Z",
          "updatedAt": "2026-10-31T00:00:00.000Z",
          "memoryAt": "2026-11-03T00:00:00.000Z",
          "showAt": "2026-10-31T00:00:00.000Z"
        }
        """;

    private static (ImmichApi api, List<HttpRequestMessage> requests) ApiReturning(string json)
    {
        var requests = new List<HttpRequestMessage>();
        var handler = new StubHandler(request =>
        {
            requests.Add(request);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        return (new ImmichApi("http://immich.test", new HttpClient(handler)), requests);
    }

    [Test]
    public async Task SearchMemoriesAsync_DeserializesBirthdayMemories()
    {
        var json = $"[{Memory("on_this_day", """{"year":2020}""")},{Memory("birthday", """{"year":1989,"personId":"c82777cf-ef51-48b2-8e9e-1fc7d20beacf","personName":"Sara"}""")}]";
        var (api, requests) = ApiReturning(json);

        var memories = (await api.SearchMemoriesAsync(new DateTimeOffset(2026, 10, 31, 14, 30, 0, TimeSpan.Zero), null, null, null, null, null, null, null, null)).ToList();

        Assert.That(memories.Select(m => m.Type), Is.EqualTo(new[] { MemoryType.On_this_day, MemoryType.Birthday }));
        Assert.That(memories[1].Data.PersonName, Is.EqualTo("Sara"));
        Assert.That(memories[1].Data.Year, Is.EqualTo(1989));
        Assert.That(requests.Single().RequestUri!.Query, Does.Contain("for=2026-10-31").And.Not.Contain("T14"));
    }

    [Test]
    public async Task Pool_ReturnsNoAssetsInsteadOfThrowing_WhenImmichSendsUnknownMemoryType()
    {
        var (api, _) = ApiReturning($"[{Memory("some_future_type", """{"year":2020}""")}]");
        var pool = new MemoryAssetsPool(api, new Mock<IAccountSettings>().Object);

        var assets = await pool.GetAssets(10, CancellationToken.None);

        Assert.That(assets, Is.Empty);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
