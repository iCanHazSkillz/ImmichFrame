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
        var albums = AccountSettings.Albums;
        if (albums == null)
        {
            return [];
        }

        // Each configured album is paginated independently; fetch them concurrently (up to a
        // shared limit) instead of one at a time so accounts with many configured albums don't
        // pay for N sequential paginated fetches in a row.
        var perAlbumAssets = await AssetHelper.RunWithConcurrencyLimitAsync(albums, albumId => LoadAlbumAssets(albumId, ct));

        return perAlbumAssets.SelectMany(assets => assets);
    }

    private async Task<List<AssetResponseDto>> LoadAlbumAssets(Guid albumId, CancellationToken ct)
    {
        var albumAssets = new List<AssetResponseDto>();

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

            if (!AccountSettings.ShowVideos)
            {
                metadataBody.Type = AssetTypeEnum.IMAGE;
            }

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

        return albumAssets;
    }
}
