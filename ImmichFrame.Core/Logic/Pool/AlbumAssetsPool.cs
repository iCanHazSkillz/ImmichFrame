using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Logic.Pool;

public class AlbumAssetsPool : CachingApiAssetsPool
{
    public AlbumAssetsPool(IApiCache apiCache, ImmichApi immichApi, IAccountSettings accountSettings, ILogger<AlbumAssetsPool>? logger = null)
        : base(apiCache, immichApi, accountSettings, logger)
    {
    }

    protected override async Task<IEnumerable<AssetResponseDto>> LoadAssets(CancellationToken ct = default)
    {
        var albumAssets = new List<AssetResponseDto>();

        var albums = AccountSettings.Albums;
        if (albums != null)
        {
            foreach (var albumId in albums)
            {
                AlbumResponseDto albumInfo;
                try
                {
                    albumInfo = await ImmichApi.GetAlbumInfoAsync(albumId, null, null, ct);
                }
                catch (ApiException ex) when (AssetHelper.IsExpectedAlbumLookupFailure(ex))
                {
                    AssetHelper.LogSkippedAlbum(Logger, albumId, AccountSettings.ImmichServerUrl, "included", ex);
                    continue;
                }

                if (albumInfo.Assets != null)
                {
                    albumAssets.AddRange(albumInfo.Assets);
                }
            }
        }

        return albumAssets;
    }
}
