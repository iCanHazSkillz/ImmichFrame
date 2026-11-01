using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Logic.Pool;

public class MemoryAssetsPool : CachingApiAssetsPool
{
    public MemoryAssetsPool(ImmichApi immichApi, IAccountSettings accountSettings, ILogger? logger = null)
        : base(new DailyApiCache(), immichApi, accountSettings, logger)
    {
    }

    protected override async Task<IEnumerable<AssetResponseDto>> LoadAssets(CancellationToken ct = default)
    {
        var searchDate = DateTimeOffset.Now;

        ICollection<MemoryResponseDto> memories;
        try
        {
            memories = await ImmichApi.SearchMemoriesAsync(searchDate, null, null, null, null, null, null, null, null, ct);
        }
        catch (ApiException ex) when (ex.InnerException is Newtonsoft.Json.JsonException)
        {
            // Immich answered successfully but with a shape this build's generated client can't
            // read (e.g. a memory type added in a newer Immich release, like "birthday" in v3.3).
            // Drop memories for today instead of failing the whole asset list with a 500.
            Logger?.LogError(ex,
                "Could not read memories from Immich at {immichServerUrl}; the Immich server is probably newer than the bundled OpenAPI spec. Memories are skipped until the spec is updated.",
                AccountSettings.ImmichServerUrl);
            return [];
        }

        var memoryAssets = new List<AssetResponseDto>();
        foreach (var memory in memories)
        {
            var assets = memory.Assets.ToList();
            var description = DescribeMemory(memory, searchDate);

            if (!AccountSettings.ShowVideos)
            {
                assets = assets.Where(a => a.Type == AssetTypeEnum.IMAGE).ToList();
            }

            foreach (var asset in assets)
            {
                if (asset.ExifInfo == null)
                {
                    var assetInfo = await ImmichApi.GetAssetInfoAsync(asset.Id, null, null, ct);
                    asset.ExifInfo = assetInfo.ExifInfo;
                    asset.People = assetInfo.People;
                }

                asset.ExifInfo.Description = description;
            }

            memoryAssets.AddRange(assets);
        }

        return memoryAssets;
    }

    private static string DescribeMemory(MemoryResponseDto memory, DateTimeOffset searchDate)
    {
        if (memory.Type == MemoryType.Birthday)
        {
            // For birthday memories Data.Year is the person's birth year, not the photo year,
            // so "N years ago" would be wrong.
            return string.IsNullOrWhiteSpace(memory.Data.PersonName)
                ? "Birthday"
                : $"{memory.Data.PersonName}'s birthday";
        }

        var yearsAgo = searchDate.Year - memory.Data.Year;
        return $"{yearsAgo} {(yearsAgo == 1 ? "year" : "years")} ago";
    }
}

class DailyApiCache : ApiCache
{
    public DailyApiCache() : base(() => new MemoryCacheEntryOptions
    {
        AbsoluteExpiration = DateTimeOffset.Now.Date.AddDays(1)
    }
    )
    {
    }
}
