using System.Diagnostics;
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Logic.Pool;

/// <summary>
/// Picks assets for an account with Immich's server-side search (v3.2+): one random-search call
/// per batch, filtered by Immich, instead of downloading and filtering every configured album,
/// person and tag. Replaces the former album/people/favorite/tag/all-assets pools.
/// Ported from upstream immichFrame/ImmichFrame (#712).
/// </summary>
public class AccountSearchPool(IApiCache apiCache, ImmichApi immichApi, IAccountSettings accountSettings, ILogger<AccountSearchPool>? logger = null) : IAssetPool
{
    // Matches CachingApiAssetsPool: above this, a fetch is logged at Warning.
    private static readonly TimeSpan SlowFetchThreshold = TimeSpan.FromSeconds(3);

    public async Task<long> GetAssetCount(CancellationToken ct = default)
    {
        var tagIds = await ResolveTagIds(ct);
        if (SearchFilters.HasConfiguredSource(accountSettings) && !SearchFilters.HasMatchingBranch(accountSettings, tagIds))
        {
            return 0;
        }

        var filter = SearchFilters.ForAccount(accountSettings, tagIds);
        return await apiCache.GetOrAddAsync($"{nameof(AccountSearchPool)}.stats", () => Timed("asset count", async () =>
        {
            var stats = await immichApi.SearchAssetStatisticsAsync(new StatisticsSearchDto { Filter = filter }, ct);
            return stats.Total;
        }));
    }

    public async Task<IEnumerable<AssetResponseDto>> GetAssets(int requested, CancellationToken ct = default)
    {
        var tagIds = await ResolveTagIds(ct);
        if (SearchFilters.HasConfiguredSource(accountSettings) && !SearchFilters.HasMatchingBranch(accountSettings, tagIds))
        {
            return [];
        }

        var assets = await Timed("random assets", () => immichApi.SearchRandomAsync(new RandomSearchDto
        {
            Size = requested,
            WithExif = true,
            WithPeople = true,
            Filter = SearchFilters.ForAccount(accountSettings, tagIds)
        }, ct));

        if (tagIds.Count > 0)
        {
            await AttachConfiguredTags(assets, tagIds, ct);
        }

        return assets;
    }

    private async Task<IReadOnlyList<Guid>> ResolveTagIds(CancellationToken ct)
    {
        if (accountSettings.Tags is not { Count: > 0 } values)
        {
            return [];
        }

        // Exact, case-sensitive match on the full tag value (e.g. "Parent/Child"), as before.
        var allTags = await apiCache.GetOrAddAsync($"allTags_{accountSettings.ImmichServerUrl}",
            () => immichApi.GetAllTagsAsync(ct));
        var tagValueToTag = allTags.ToDictionary(t => t.Value);
        return values
            .Where(tagValueToTag.ContainsKey)
            .Select(value => tagValueToTag[value].Id)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Random search doesn't return tags, but the frame's "show tags" caption reads them from the
    /// asset list. Keep the old tag pool's behavior: attach the configured tags each asset has.
    /// Only runs when the account filters by tags.
    /// </summary>
    private async Task AttachConfiguredTags(IEnumerable<AssetResponseDto> assets, IReadOnlyList<Guid> tagIds, CancellationToken ct)
    {
        var configured = tagIds.ToHashSet();
        await AssetHelper.RunWithConcurrencyLimitAsync(assets.ToList(), async asset =>
        {
            var info = await immichApi.GetAssetInfoAsync(asset.Id, null, null, ct);
            asset.Tags = (info.Tags ?? []).Where(tag => configured.Contains(tag.Id)).ToList();
            return asset;
        });
    }

    private async Task<T> Timed<T>(string what, Func<Task<T>> fetch)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await fetch();
        stopwatch.Stop();

        if (logger != null && stopwatch.Elapsed > SlowFetchThreshold)
        {
            logger.LogWarning("{pool} took {elapsedMs}ms to fetch {what} for account {immichServerUrl}",
                nameof(AccountSearchPool), (long)stopwatch.Elapsed.TotalMilliseconds, what, accountSettings.ImmichServerUrl);
        }

        return result;
    }
}
