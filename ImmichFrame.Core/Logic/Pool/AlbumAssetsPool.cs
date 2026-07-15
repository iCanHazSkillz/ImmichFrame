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
                int page = 1;
                int batchSize = 1000;
                int itemsInPage;
                do
                {
                    var metadataBody = new MetadataSearchDto
                    {
                        Page = page,
                        Size = batchSize,
                        AlbumIds = [albumId],
                        WithExif = true,
                        WithPeople = true,
                    };

                    SearchResponseDto searchResponse;
                    try
                    {
                        searchResponse = await ImmichApi.SearchAssetsAsync(null, null, metadataBody, ct);
                    }
                    catch (ApiException ex) when (AssetHelper.IsExpectedAlbumLookupFailure(ex))
                    {
                        AssetHelper.LogSkippedAlbum(Logger, albumId, AccountSettings.ImmichServerUrl, "included", ex);
                        break;
                    }

                    itemsInPage = searchResponse.Assets.Items.Count;

                    albumAssets.AddRange(searchResponse.Assets.Items);
                    page++;
                } while (itemsInPage == batchSize);
            }
        }

        return albumAssets;
    }
}
