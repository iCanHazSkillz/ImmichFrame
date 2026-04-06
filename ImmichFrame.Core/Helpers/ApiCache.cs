using Microsoft.Extensions.Caching.Memory;

namespace ImmichFrame.Core.Helpers;

public class ApiCache : IApiCache, IDisposable
{
    private readonly Func<MemoryCacheEntryOptions> _cacheOptions;
    private readonly object _sync = new();
    private IMemoryCache? _cache = new MemoryCache(new MemoryCacheOptions());
    private int _leaseCount;
    private bool _disposeRequested;

    public ApiCache(TimeSpan cacheDuration) : this(() => new MemoryCacheEntryOptions()
    {
        AbsoluteExpirationRelativeToNow = cacheDuration
    })
    {
    }

    public ApiCache(Func<MemoryCacheEntryOptions> entryOptions)
    {
        _cacheOptions = entryOptions;
    }

    public virtual async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory) where T : notnull
    {
        using var lease = AcquireLease();
        IMemoryCache cache;
        lock (_sync)
        {
            cache = _cache ?? throw new ObjectDisposedException(nameof(ApiCache));
        }

        var value = await cache.GetOrCreateAsync<T>(key, _ => factory(), _cacheOptions());
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }

    public IDisposable AcquireLease()
    {
        lock (_sync)
        {
            _ = _cache ?? throw new ObjectDisposedException(nameof(ApiCache));
            _leaseCount++;
            return new CacheLease(this);
        }
    }

    public void Dispose()
    {
        IMemoryCache? cacheToDispose = null;
        lock (_sync)
        {
            if (_cache is null)
            {
                return;
            }

            _disposeRequested = true;
            if (_leaseCount == 0)
            {
                cacheToDispose = _cache;
                _cache = null;
            }
        }

        cacheToDispose?.Dispose();
    }

    private void ReleaseLease()
    {
        IMemoryCache? cacheToDispose = null;
        lock (_sync)
        {
            if (_leaseCount > 0)
            {
                _leaseCount--;
            }

            if (_disposeRequested && _leaseCount == 0 && _cache is not null)
            {
                cacheToDispose = _cache;
                _cache = null;
            }
        }

        cacheToDispose?.Dispose();
    }

    private sealed class CacheLease(ApiCache owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.ReleaseLease();
            }
        }
    }
}
