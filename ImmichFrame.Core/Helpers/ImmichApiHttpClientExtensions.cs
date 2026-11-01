using Microsoft.Extensions.DependencyInjection;

namespace ImmichFrame.Core.Helpers;

public static class ImmichApiHttpClientExtensions
{
    public const string ImmichApiAccountClient = "ImmichApiAccountClient";

    // Every call to Immich (metadata, pagination, image/video fetches) goes through this named
    // client. Without an explicit timeout it falls back to HttpClient's 100s default, so a stalled
    // connection can hang a request for a long time; bounding it makes a stuck connection a prompt,
    // retryable failure. Video/range requests use ResponseHeadersRead, so this only bounds
    // time-to-headers for those, not the full streamed download.
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Registers the HTTP client that carries the Immich API key. The key travels in the
    /// non-standard X-API-KEY header, which the runtime does not treat as a credential and would
    /// replay to whatever host a redirect points at, so redirects are never followed.
    /// </summary>
    public static IHttpClientBuilder AddImmichApiHttpClient(this IServiceCollection services)
    {
        services.AddTransient<RedirectLoggingHandler>();

        return services.AddHttpClient(ImmichApiAccountClient, client => client.Timeout = RequestTimeout)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .AddHttpMessageHandler<RedirectLoggingHandler>();
    }
}
