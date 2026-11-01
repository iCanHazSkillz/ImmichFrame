using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;

namespace ImmichFrame.Core.Logic.Pool;

public class MultiAssetPool(IEnumerable<IAssetPool> delegates) : AggregatingAssetPool
{
    // A pool may return fewer assets than its count suggested (e.g. it ran dry). Re-allocate the
    // shortfall across the pools a bounded number of times rather than looping forever.
    private const int MaxRounds = 5;

    private readonly IReadOnlyList<IAssetPool> _delegates = delegates.ToList();
    private readonly Random _random = new();

    public override async Task<long> GetAssetCount(CancellationToken ct = default)
    {
        var counts = _delegates.Select(pool => pool.GetAssetCount(ct));
        return (await Task.WhenAll(counts)).Sum();
    }

    /// <summary>
    /// Fetches a batch with one call per pool instead of one call per asset. Each slot of the
    /// batch is assigned to a pool with probability proportional to the pool's asset count (the
    /// same weighting as picking assets one at a time), then every pool is asked once for its
    /// share. With server-side search pools this turns N sequential Immich round-trips into one
    /// per pool.
    /// </summary>
    public override async Task<IEnumerable<AssetResponseDto>> GetAssets(int requested, CancellationToken ct = default)
    {
        var collected = new List<AssetResponseDto>(Math.Max(requested, 0));

        for (var round = 0; round < MaxRounds && collected.Count < requested; round++)
        {
            var counts = await Task.WhenAll(_delegates.Select(pool => pool.GetAssetCount(ct)));
            var total = counts.Sum(count => Math.Max(count, 0));
            if (total <= 0)
            {
                break;
            }

            var remaining = requested - collected.Count;
            var shares = Allocate(counts, total, remaining);

            var batches = await Task.WhenAll(_delegates.Select((pool, i) => shares[i] > 0
                ? pool.GetAssets(shares[i], ct)
                : Task.FromResult(Enumerable.Empty<AssetResponseDto>())));

            var received = batches.SelectMany(batch => batch ?? []).Take(remaining).ToList();
            if (received.Count == 0)
            {
                break;
            }

            collected.AddRange(received);
        }

        return collected.Shuffle().ToList();
    }

    private int[] Allocate(long[] counts, long total, int slots)
    {
        var shares = new int[counts.Length];
        for (var slot = 0; slot < slots; slot++)
        {
            var pick = _random.NextInt64(total);
            for (var i = 0; i < counts.Length; i++)
            {
                var weight = Math.Max(counts[i], 0);
                if (pick < weight)
                {
                    shares[i]++;
                    break;
                }

                pick -= weight;
            }
        }

        return shares;
    }

    protected override async Task<AssetResponseDto?> GetNextAsset(CancellationToken ct)
        => (await GetAssets(1, ct)).FirstOrDefault();
}
