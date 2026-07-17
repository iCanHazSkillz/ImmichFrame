using ImmichFrame.Core.Helpers;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Helpers;

[TestFixture]
public class ApiCacheTests
{
    [Test]
    public async Task GetOrAddAsync_ConcurrentCallsSameKey_InvokesFactoryOnce()
    {
        var factoryCalls = 0;
        var factoryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cache = new ApiCache(TimeSpan.FromMinutes(5));

        async Task<int> Factory()
        {
            Interlocked.Increment(ref factoryCalls);
            factoryStarted.TrySetResult();
            await releaseGate.Task;
            return 42;
        }

        // Start the first call and wait for Factory to actually begin running - by then the
        // in-flight entry is guaranteed registered, so every joiner started afterward is
        // guaranteed to coalesce onto it rather than possibly racing ahead of it.
        var firstCall = cache.GetOrAddAsync("key", Factory);
        await factoryStarted.Task;

        var joinerCalls = Enumerable
            .Range(0, 19)
            .Select(_ => cache.GetOrAddAsync("key", Factory))
            .ToList();

        releaseGate.SetResult();
        var results = await Task.WhenAll(new[] { firstCall }.Concat(joinerCalls));

        Assert.That(factoryCalls, Is.EqualTo(1));
        Assert.That(results, Has.All.EqualTo(42));
    }

    [Test]
    public async Task GetOrAddAsync_ConcurrentCallsSameKey_AllJoinersObserveSameException()
    {
        var factoryCalls = 0;
        var factoryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thrown = new InvalidOperationException("boom");
        using var cache = new ApiCache(TimeSpan.FromMinutes(5));

        async Task<int> Factory()
        {
            Interlocked.Increment(ref factoryCalls);
            factoryStarted.TrySetResult();
            await releaseGate.Task;
            throw thrown;
        }

        var caught = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        async Task Call()
        {
            try
            {
                await cache.GetOrAddAsync("key", Factory);
            }
            catch (Exception ex)
            {
                caught.Add(ex);
            }
        }

        // Same deterministic arrangement as the success-case test above: the first call starts
        // the in-flight entry, and every joiner is only started once that entry is guaranteed
        // registered, so all 20 are guaranteed to observe the one factory invocation's outcome.
        var firstCall = Call();
        await factoryStarted.Task;

        var joinerCalls = Enumerable.Range(0, 19).Select(_ => Call()).ToList();

        releaseGate.SetResult();
        await Task.WhenAll(new[] { firstCall }.Concat(joinerCalls));

        Assert.That(factoryCalls, Is.EqualTo(1));
        Assert.That(caught, Has.Count.EqualTo(20));
        Assert.That(caught, Has.All.SameAs(thrown));
    }

    [Test]
    public async Task GetOrAddAsync_AfterFactoryFailure_RetriesCleanly()
    {
        var calls = 0;
        using var cache = new ApiCache(TimeSpan.FromMinutes(5));

        Task<int> Factory()
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("transient");
            }

            return Task.FromResult(7);
        }

        Assert.ThrowsAsync<InvalidOperationException>(async () => await cache.GetOrAddAsync("key", Factory));

        int second = 0;
        Assert.DoesNotThrowAsync(async () => second = await cache.GetOrAddAsync("key", Factory));
        Assert.That(second, Is.EqualTo(7));

        var third = await cache.GetOrAddAsync("key", Factory);
        Assert.That(third, Is.EqualTo(7));
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public async Task GetOrAddAsync_DifferentKeys_DoNotBlockEachOther()
    {
        using var cache = new ApiCache(TimeSpan.FromMinutes(5));
        var gateA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var taskA = cache.GetOrAddAsync("A", async () =>
        {
            await gateA.Task;
            return "a";
        });
        var taskB = cache.GetOrAddAsync("B", () => Task.FromResult("b"));

        var completed = await Task.WhenAny(taskB, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.That(completed, Is.SameAs(taskB));
        Assert.That(await taskB, Is.EqualTo("b"));

        gateA.SetResult();
        Assert.That(await taskA, Is.EqualTo("a"));
    }

    [Test]
    public async Task GetOrAddAsync_SequentialCallsSameKey_UsesCacheAfterFirstFetch()
    {
        var calls = 0;
        using var cache = new ApiCache(TimeSpan.FromMinutes(5));

        Task<int> Factory()
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(9);
        }

        var first = await cache.GetOrAddAsync("key", Factory);
        var second = await cache.GetOrAddAsync("key", Factory);
        var third = await cache.GetOrAddAsync("key", Factory);

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(new[] { first, second, third }, Has.All.EqualTo(9));
    }

    [Test]
    public async Task GetOrAddAsync_AfterExpiry_StartsNewGenerationAndInvokesFactoryAgain()
    {
        var calls = 0;
        using var cache = new ApiCache(() => new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(30)
        });

        Task<int> Factory() => Task.FromResult(Interlocked.Increment(ref calls));

        var first = await cache.GetOrAddAsync("key", Factory);
        await Task.Delay(150);
        var second = await cache.GetOrAddAsync("key", Factory);

        Assert.That(calls, Is.EqualTo(2));
        Assert.That(second, Is.Not.EqualTo(first));
    }

    [Test]
    public async Task Dispose_WhileFetchInFlight_DoesNotDisposeCacheUntilInFlightCompletes()
    {
        var cache = new ApiCache(TimeSpan.FromMinutes(5));
        var releaseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var inFlight = cache.GetOrAddAsync("key", async () =>
        {
            await releaseGate.Task;
            return 1;
        });

        Assert.DoesNotThrow(() => cache.Dispose());

        releaseGate.SetResult();
        Assert.That(await inFlight, Is.EqualTo(1));

        Assert.ThrowsAsync<ObjectDisposedException>(async () => await cache.GetOrAddAsync("key", () => Task.FromResult(2)));
    }

    [Test]
    public async Task GetOrAddAsync_KeyReusedWithDifferentType_ThrowsWithoutDisturbingOriginalInFlightCall()
    {
        using var cache = new ApiCache(TimeSpan.FromMinutes(5));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var stringTask = cache.GetOrAddAsync("key", async () =>
        {
            await gate.Task;
            return "value";
        });

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await cache.GetOrAddAsync("key", () => Task.FromResult(1)));

        gate.SetResult();
        Assert.That(await stringTask, Is.EqualTo("value"));
    }
}
