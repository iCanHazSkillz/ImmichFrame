using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Logic.Pool;

public class FavoriteAssetsPool : CachingApiAssetsPool
{
    public FavoriteAssetsPool(IApiCache apiCache, ImmichApi immichApi, IAccountSettings accountSettings, ILogger<FavoriteAssetsPool>? logger = null)
        : base(apiCache, immichApi, accountSettings, logger)
    {
    }

    protected override async Task<IEnumerable<AssetResponseDto>> LoadAssets(CancellationToken ct = default)
    {
        var favoriteAssets = new List<AssetResponseDto>();

        int page = 1;
        int batchSize = 1000;
        int itemsInPage;
        do
        {
            var metadataBody = new MetadataSearchDto
            {
                Page = page,
                Size = batchSize,
                IsFavorite = true,
                WithExif = true,
                WithPeople = true
            };

            if (!AccountSettings.ShowVideos)
            {
                metadataBody.Type = AssetTypeEnum.IMAGE;
            }

            var favoriteInfo = await ImmichApi.SearchAssetsAsync(null, null, metadataBody, ct);

            itemsInPage = favoriteInfo.Assets.Items.Count;

            favoriteAssets.AddRange(favoriteInfo.Assets.Items);
            page++;
        } while (itemsInPage == batchSize);

        return favoriteAssets;
    }
}
