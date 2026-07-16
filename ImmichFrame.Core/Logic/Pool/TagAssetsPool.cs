using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Logic.Pool;

public class TagAssetsPool : CachingApiAssetsPool
{
    public TagAssetsPool(IApiCache apiCache, ImmichApi immichApi, IAccountSettings accountSettings, ILogger<TagAssetsPool>? logger = null)
        : base(apiCache, immichApi, accountSettings, logger)
    {
    }

    protected override async Task<IEnumerable<AssetResponseDto>> LoadAssets(CancellationToken ct = default)
    {
        var tagAssets = new List<AssetResponseDto>();

        if (AccountSettings.Tags == null)
        {
            return tagAssets;
        }

        var allTags = await ApiCache.GetOrAddAsync(
            $"allTags_{AccountSettings.ImmichServerUrl}",
            () => ImmichApi.GetAllTagsAsync(ct));
        var tagValueToTag = allTags.ToDictionary(t => t.Value);

        // Find the tags for the configured tag values
        var tags = new List<TagResponseDto>();
        foreach (var tagValue in AccountSettings.Tags)
        {
            if (tagValueToTag.TryGetValue(tagValue, out var tag))
            {
                tags.Add(tag);
            }
        }

        // Each configured tag is paginated independently; fetch them concurrently instead of one
        // at a time so accounts with many configured tags don't pay for N sequential paginated
        // fetches in a row. Results are merged afterward (sequentially) since an asset matching
        // multiple tags needs every matching tag attached.
        var perTagAssets = await Task.WhenAll(tags.Select(tag => LoadTagAssets(tag, ct)));

        var assetById = new Dictionary<Guid, AssetResponseDto>();
        foreach (var results in perTagAssets)
        {
            foreach (var asset in results)
            {
                if (assetById.TryGetValue(asset.Id, out var existing))
                {
                    existing.Tags.Add(asset.Tags.Single());
                    continue;
                }

                assetById[asset.Id] = asset;
                tagAssets.Add(asset);
            }
        }

        return tagAssets;
    }

    private async Task<List<AssetResponseDto>> LoadTagAssets(TagResponseDto tag, CancellationToken ct)
    {
        var results = new List<AssetResponseDto>();

        int page = 1;
        int batchSize = 1000;
        int itemsInPage;
        do
        {
            var metadataBody = new MetadataSearchDto
            {
                Page = page,
                Size = batchSize,
                TagIds = [tag.Id],
                WithExif = true,
                WithPeople = true
            };

            if (!AccountSettings.ShowVideos)
            {
                metadataBody.Type = AssetTypeEnum.IMAGE;
            }

            var tagInfo = await ImmichApi.SearchAssetsAsync(null, null, metadataBody, ct);

            itemsInPage = tagInfo.Assets.Items.Count;

            foreach (var asset in tagInfo.Assets.Items)
            {
                // SearchAssetsAsync does not support a `WithTags` parameter, so simply set the
                // one that was configured; matches against other configured tags are merged by
                // the caller once every tag's fetch has completed.
                asset.Tags = new List<TagResponseDto> { tag };
                results.Add(asset);
            }

            page++;
        } while (itemsInPage == batchSize);

        return results;
    }
}
