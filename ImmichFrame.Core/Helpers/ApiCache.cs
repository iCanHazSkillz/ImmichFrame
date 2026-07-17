using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace ImmichFrame.Core.Helpers;

public class ApiCache : IApiCache, IDisposable
{
    private readonly Func<MemoryCacheEntryOptions> _cacheOptions;
    private readonly object _sync = new();
    private IMemoryCache? _cache = new MemoryCache(new MemoryCacheOptions());
    private int _leaseCount;
    private bool _disposeRequested;

    // Coalesces concurrent cache-miss callers for the same key onto a single in-flight
    // Task<T>, so N racing callers invoke factory() once instead of N times. Entries exist only
    // while work for that key is in flight - the caller that reaches the entry's completion
    // removes it in a finally block, so a fresh generation starts cleanly on the next miss
    // (post-expiry or post-failure) and this never becomes a second long-term cache alongside
    // IMemoryCache. Values are stored as `object` because one dictionary has to serve every T
    // used across all cache keys; the stored value is always a Lazy<Task<T>> reference, so this
    // is a reference-type upcast, not boxing.
    private readonly ConcurrentDictionary<string, object> _inFlight = new();

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

        if (cache.TryGetValue(key, out T cached))
        {
            ArgumentNullException.ThrowIfNull(cached);
            return cached;
        }

        var lazy = GetOrCreateInFlightEntry(key, cache, factory);
        try
        {
            return await lazy.Value;
        }
        finally
        {
            _inFlight.TryRemove(new KeyValuePair<string, object>(key, lazy));
        }
    }

    private Lazy<Task<T>> GetOrCreateInFlightEntry<T>(string key, IMemoryCache cache, Func<Task<T>> factory) where T : notnull
    {
        var candidate = new Lazy<Task<T>>(
            () => RunAndCacheAsync(key, cache, factory),
            LazyThreadSafetyMode.ExecutionAndPublication);

        var stored = _inFlight.GetOrAdd(key, candidate);

        if (stored is not Lazy<Task<T>> lazy)
        {
            throw new InvalidOperationException(
                $"ApiCache key '{key}' is already in flight for a different result type ({stored.GetType()}); expected Lazy<Task<{typeof(T)}>>.");
        }

        return lazy;
    }

    // Runs exactly once per key per "generation": Lazy<T>'s ExecutionAndPublication mode
    // guarantees only one caller's thread invokes this for a given Lazy<Task<T>> instance, and
    // every concurrent caller for that key awaits the same returned Task<T>. `cache` is safe to
    // use here without re-locking: every caller awaiting this method's Task - including whichever
    // caller is executing it - is still holding the lease it acquired in GetOrAddAsync (leases
    // are only released after that caller's `await lazy.Value` returns), so Dispose() cannot have
    // nulled out/disposed _cache while this method is running.
    private async Task<T> RunAndCacheAsync<T>(string key, IMemoryCache cache, Func<Task<T>> factory) where T : notnull
    {
        var value = await factory();
        ArgumentNullException.ThrowIfNull(value);
        cache.Set(key, value, _cacheOptions());
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
