using System.Net;
using System.Net.Http.Json;
using System.Collections;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.WebApi.Models;
using ImmichFrame.WebApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.Controllers;

[TestFixture]
public class FrameSessionsControllerTests
{
    private const string AppInstanceHeaderName = "X-ImmichFrame-Instance";
    private WebApplicationFactory<Program> _factory = null!;
    private string _tempAppDataPath = null!;

    [SetUp]
    public void Setup()
    {
        _tempAppDataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempAppDataPath);

        _factory = CreateFactory(_tempAppDataPath);
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
    public async Task AdminEndpoints_RequireAuthentication()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/admin/frame-sessions");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task AdminAuthSession_ReturnsAuthenticatedUserAfterLogin()
    {
        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });

        var sessionBeforeLogin = await adminClient.GetFromJsonAsync<AdminAuthSessionDto>("/api/admin/auth/session");
        Assert.That(sessionBeforeLogin, Is.Not.Null);
        Assert.That(sessionBeforeLogin!.IsConfigured, Is.True);
        Assert.That(sessionBeforeLogin.IsAuthenticated, Is.False);

        await LoginAdminAsync(adminClient);

        var sessionAfterLogin = await adminClient.GetFromJsonAsync<AdminAuthSessionDto>("/api/admin/auth/session");
        Assert.That(sessionAfterLogin, Is.Not.Null);
        Assert.That(sessionAfterLogin!.IsAuthenticated, Is.True);
        Assert.That(sessionAfterLogin.Username, Is.EqualTo("admin"));
    }

    [Test]
    public async Task SessionLifecycle_ReturnsActiveSessionsAndCommands()
    {
        var frameClient = _factory.CreateClient();
        frameClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-secret");

        var snapshot = new FrameSessionSnapshotDto
        {
            PlaybackState = FramePlaybackState.Playing,
            Status = FrameSessionStatus.Active,
            CurrentDisplay = new DisplayEventDto
            {
                DisplayedAtUtc = DateTimeOffset.UtcNow,
                DurationSeconds = 20,
                Assets =
                [
                    new DisplayedAssetDto
                    {
                        Id = "asset-1",
                        OriginalFileName = "current.jpg",
                        Type = ImmichFrame.Core.Api.AssetTypeEnum.IMAGE,
                        ImmichServerUrl = "http://mock-immich-server.com"
                    }
                ]
            }
        };

        var putResponse = await frameClient.PutAsJsonAsync("/api/frame-sessions/frame-1", snapshot);
        putResponse.EnsureSuccessStatusCode();

        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var adminResponse = await adminClient.GetFromJsonAsync<List<FrameSessionStateDto>>("/api/admin/frame-sessions");

        Assert.That(adminResponse, Is.Not.Null);
        Assert.That(adminResponse!, Has.Count.EqualTo(1));
        Assert.That(adminResponse[0].ClientIdentifier, Is.EqualTo("frame1"));
        Assert.That(adminResponse[0].CurrentDisplay?.Assets[0].OriginalFileName, Is.EqualTo("current.jpg"));
        Assert.That(adminResponse[0].CurrentDisplay?.Assets[0].ImmichServerUrl, Is.EqualTo("http://mock-immich-server.com"));
        Assert.That(adminResponse[0].CurrentDisplay?.DurationSeconds, Is.EqualTo(20));

        var commandResponse = await adminClient.PostAsJsonAsync("/api/admin/frame-sessions/frame-1/commands", new EnqueueAdminCommandRequest
        {
            CommandType = FrameAdminCommandType.Next
        });
        commandResponse.EnsureSuccessStatusCode();

        var commands = await frameClient.GetFromJsonAsync<List<AdminCommandDto>>("/api/frame-sessions/frame-1/commands");

        Assert.That(commands, Is.Not.Null);
        Assert.That(commands!, Has.Count.EqualTo(1));
        Assert.That(commands[0].CommandType, Is.EqualTo(FrameAdminCommandType.Next));

        var ackResponse = await frameClient.PostAsync($"/api/frame-sessions/frame-1/commands/{commands[0].CommandId}/ack", null);
        ackResponse.EnsureSuccessStatusCode();

        var commandsAfterAck = await frameClient.GetFromJsonAsync<List<AdminCommandDto>>("/api/frame-sessions/frame-1/commands");
        Assert.That(commandsAfterAck, Is.Empty);
    }

    [Test]
    public async Task FrameSessionResponses_IncludeStableAppInstanceHeader()
    {
        var frameClient = _factory.CreateClient();
        frameClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-secret");

        var snapshot = new FrameSessionSnapshotDto
        {
            PlaybackState = FramePlaybackState.Playing,
            Status = FrameSessionStatus.Active,
            History = []
        };

        var putResponse = await frameClient.PutAsJsonAsync("/api/frame-sessions/frame-instance", snapshot);
        putResponse.EnsureSuccessStatusCode();
        var putHeaderValue = GetRequiredHeaderValue(putResponse, AppInstanceHeaderName);

        var commandsResponse = await frameClient.GetAsync("/api/frame-sessions/frame-instance/commands");
        commandsResponse.EnsureSuccessStatusCode();
        var commandsHeaderValue = GetRequiredHeaderValue(commandsResponse, AppInstanceHeaderName);

        Assert.That(putHeaderValue, Is.Not.Empty);
        Assert.That(commandsHeaderValue, Is.EqualTo(putHeaderValue));
    }

    [Test]
    public async Task SeparateHosts_ExposeDifferentAppInstanceHeaders()
    {
        var secondTempAppDataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(secondTempAppDataPath);

        using var secondFactory = CreateFactory(secondTempAppDataPath);
        try
        {
            var firstFrameClient = _factory.CreateClient();
            firstFrameClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-secret");

            var secondFrameClient = secondFactory.CreateClient();
            secondFrameClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-secret");

            var snapshot = new FrameSessionSnapshotDto
            {
                PlaybackState = FramePlaybackState.Playing,
                Status = FrameSessionStatus.Active,
                History = []
            };

            var firstResponse = await firstFrameClient.PutAsJsonAsync("/api/frame-sessions/frame-a", snapshot);
            firstResponse.EnsureSuccessStatusCode();
            var firstHeaderValue = GetRequiredHeaderValue(firstResponse, AppInstanceHeaderName);

            var secondResponse = await secondFrameClient.PutAsJsonAsync("/api/frame-sessions/frame-b", snapshot);
            secondResponse.EnsureSuccessStatusCode();
            var secondHeaderValue = GetRequiredHeaderValue(secondResponse, AppInstanceHeaderName);

            Assert.That(secondHeaderValue, Is.Not.EqualTo(firstHeaderValue));
        }
        finally
        {
            if (Directory.Exists(secondTempAppDataPath))
            {
                Directory.Delete(secondTempAppDataPath, true);
            }
        }
    }

    [Test]
    public async Task HtmlShellResponses_AreServedWithNoCacheHeaders()
    {
        var secondTempAppDataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var tempWebRootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(secondTempAppDataPath);
        Directory.CreateDirectory(tempWebRootPath);
        await File.WriteAllTextAsync(Path.Combine(tempWebRootPath, "index.html"), "<!doctype html><html><body>frame shell</body></html>");

        using var htmlFactory = CreateFactory(secondTempAppDataPath, tempWebRootPath);
        try
        {
            var client = htmlFactory.CreateClient();

            var indexResponse = await client.GetAsync("/index.html");
            indexResponse.EnsureSuccessStatusCode();

            var fallbackResponse = await client.GetAsync("/frames/example");
            fallbackResponse.EnsureSuccessStatusCode();

            Assert.That(GetRequiredHeaderValue(indexResponse, "Cache-Control"), Does.Contain("no-cache"));
            Assert.That(GetRequiredHeaderValue(indexResponse, "Cache-Control"), Does.Contain("must-revalidate"));
            Assert.That(GetRequiredHeaderValue(fallbackResponse, "Cache-Control"), Does.Contain("no-cache"));
            Assert.That(GetRequiredHeaderValue(fallbackResponse, "Cache-Control"), Does.Contain("must-revalidate"));
            Assert.That(indexResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
            Assert.That(fallbackResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
        }
        finally
        {
            if (Directory.Exists(secondTempAppDataPath))
            {
                Directory.Delete(secondTempAppDataPath, true);
            }

            if (Directory.Exists(tempWebRootPath))
            {
                Directory.Delete(tempWebRootPath, true);
            }
        }
    }

    [Test]
    public async Task EnqueueCommand_ReturnsGoneForStaleSession()
    {
        var registry = _factory.Services.GetRequiredService<IFrameSessionRegistry>();
        registry.UpsertSnapshot("framestale", new FrameSessionSnapshotDto(), "NUnit-Agent");
        registry.MarkStopped("framestale", "NUnit-Agent");

        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        await LoginAdminAsync(adminClient);

        var response = await adminClient.PostAsJsonAsync(
            "/api/admin/frame-sessions/framestale/commands",
            new EnqueueAdminCommandRequest
            {
                CommandType = FrameAdminCommandType.Refresh
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
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

    private static WebApplicationFactory<Program> CreateFactory(string tempAppDataPath, string? webRootPath = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                if (!string.IsNullOrWhiteSpace(webRootPath))
                {
                    builder.UseSetting(WebHostDefaults.WebRootKey, webRootPath);
                }

                builder.ConfigureTestServices(services =>
                {
                    var generalSettings = new GeneralSettings
                    {
                        AuthenticationSecret = "test-secret",
                        PhotoDateFormat = "MM/dd/yyyy",
                        ImageLocationFormat = "City,State,Country",
                        Language = "en"
                    };

                    var accountSettings = new ServerAccountSettings
                    {
                        ImmichServerUrl = "http://mock-immich-server.com",
                        ApiKey = "test-api-key"
                    };

                    var serverSettings = new ServerSettings
                    {
                        GeneralSettingsImpl = generalSettings,
                        AccountsImpl = new List<ServerAccountSettings> { accountSettings }
                    };

                    services.AddSingleton(new BootstrapServerSettingsHolder(serverSettings));
                    services.AddSingleton(new AdminManagedSettingsStoreOptions
                    {
                        StorePath = Path.Combine(tempAppDataPath, "admin-settings.json")
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
                                DisplayNameStorePath = Path.Combine(tempAppDataPath, "frame-session-display-names.json")
                            },
                            null,
                            null));
                });
            });
    }

    private static string GetRequiredHeaderValue(HttpResponseMessage response, string headerName)
    {
        if (response.Headers.TryGetValues(headerName, out var responseHeaderValues))
        {
            return responseHeaderValues.Single();
        }

        if (response.Content.Headers.TryGetValues(headerName, out var contentHeaderValues))
        {
            return contentHeaderValues.Single();
        }

        Assert.Fail($"Expected header '{headerName}' to be present.");
        return string.Empty;
    }
}
