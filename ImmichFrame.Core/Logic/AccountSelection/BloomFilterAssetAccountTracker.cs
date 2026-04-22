using BloomFilter;
using ImmichFrame.Core.Exceptions;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Logic.AccountSelection;

public class BloomFilterAssetAccountTracker(ILogger<BloomFilterAssetAccountTracker> _logger) : IAssetAccountTracker
{
    private readonly object _sync = new();
    private readonly Dictionary<IAccountImmichFrameLogic, Task<IBloomFilter>> _logicToFilter =
        new(ReferenceEqualityComparer.Instance);

    public async ValueTask<bool> RecordAssetLocation(IAccountImmichFrameLogic account, string assetId)
    {
        Task<IBloomFilter> filterTask;
        lock (_sync)
        {
            if (!_logicToFilter.TryGetValue(account, out filterTask!))
            {
                filterTask = NewFilter(account);
                _logicToFilter.Add(account, filterTask);
            }
        }

        var filter = await filterTask;
        return await filter.AddAsync(assetId);
    }

    private static async Task<IBloomFilter> NewFilter(IImmichFrameLogic account)
    {
        return FilterBuilder.Build(await account.GetTotalAssets());
    }

    public T ForAsset<T>(string assetId, Func<IAccountImmichFrameLogic, T> f)
    {
        List<KeyValuePair<IAccountImmichFrameLogic, Task<IBloomFilter>>> filters;
        lock (_sync)
        {
            filters = _logicToFilter.ToList();
        }

        foreach (var entry in filters)
        {
            var filter = entry.Value.IsCompletedSuccessfully
                ? entry.Value.Result
                : null;

            if (filter?.Contains(assetId) == true)
            {
                try
                {
                    return f(entry.Key);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Failed to locate asset {assetId} in {entry.Key}. Must be false positive, trying next account.", assetId, entry.Key);   
                }
            }
        }
        
        _logger.LogError("Failed to locate account for asset {assetId}", assetId);
        throw new AssetNotFoundException();
    }
}
