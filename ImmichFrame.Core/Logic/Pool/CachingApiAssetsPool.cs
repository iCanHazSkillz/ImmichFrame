using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Logic.Pool;

public abstract class CachingApiAssetsPool : IAssetPool
{
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
        var excludedAlbumAssets = await ApiCache.GetOrAddAsync(
            $"{GetType().FullName}_ExcludedAlbums",
            () => AssetHelper.GetExcludedAlbumAssets(ImmichApi, AccountSettings, Logger, ct));

        return await ApiCache.GetOrAddAsync(
            GetType().FullName!,
            () => LoadAssets(ct).ApplyAccountFilters(AccountSettings, excludedAlbumAssets));
    }

    protected abstract Task<IEnumerable<AssetResponseDto>> LoadAssets(CancellationToken ct = default);
}
