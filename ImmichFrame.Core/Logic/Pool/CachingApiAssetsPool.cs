using System.Diagnostics;
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Logic.Pool;

public abstract class CachingApiAssetsPool : IAssetPool
{
    // Above this, a cache-miss fetch is logged at Warning so a slow pool shows up in production
    // logs (Information level and up) without needing Debug logging or a browser HAR capture.
    private static readonly TimeSpan SlowFetchThreshold = TimeSpan.FromSeconds(3);

    private readonly Random _random = new();

    protected CachingApiAssetsPool(IApiCache apiCache, ImmichApi immichApi, IAccountSettings accountSettings, ILogger? logger = null)
    {
        ApiCache = apiCache;
        ImmichApi = immichApi;
        AccountSettings = accountSettings;
        Logger = logger;
    }

    protected IApiCache ApiCache { get; }
    protected ImmichApi ImmichApi { get; }
    protected IAccountSettings AccountSettings { get; }
    protected ILogger? Logger { get; }

    public async Task<long> GetAssetCount(CancellationToken ct = default)
    {
        return (await AllAssets(ct)).Count();
    }

    public async Task<IEnumerable<AssetResponseDto>> GetAssets(int requested, CancellationToken ct = default)
    {
        return (await AllAssets(ct)).OrderBy(_ => _random.Next()).Take(requested);
    }

    private async Task<IEnumerable<AssetResponseDto>> AllAssets(CancellationToken ct = default)
    {
        // Keyed independently of the concrete pool type: every pool for this account excludes
        // the same albums, and they share ApiCache, so this lets them share one fetch instead of
        // each pool re-paginating the same excluded-album assets from Immich.
        var excludedAlbumAssets = await TimedFetch(
            "excluded-album assets",
            () => ApiCache.GetOrAddAsync(
                "ExcludedAlbumAssets",
                () => AssetHelper.GetExcludedAlbumAssets(ImmichApi, AccountSettings, Logger, ct)));

        return await TimedFetch(
            "assets",
            () => ApiCache.GetOrAddAsync(
                GetType().FullName!,
                () => LoadAssets(ct).ApplyAccountFilters(AccountSettings, excludedAlbumAssets)));
    }

    private async Task<T> TimedFetch<T>(string what, Func<Task<T>> fetch)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await fetch();
        stopwatch.Stop();

        if (Logger != null && stopwatch.Elapsed > SlowFetchThreshold)
        {
            Logger.LogWarning(
                "{pool} took {elapsedMs}ms to fetch {what} for account {immichServerUrl} (a cache hit would be near-instant, so this reflects an Immich round-trip, not necessarily a repeat cost)",
                GetType().Name,
                (long)stopwatch.Elapsed.TotalMilliseconds,
                what,
                AccountSettings.ImmichServerUrl);
        }

        return result;
    }

    protected abstract Task<IEnumerable<AssetResponseDto>> LoadAssets(CancellationToken ct = default);
}
