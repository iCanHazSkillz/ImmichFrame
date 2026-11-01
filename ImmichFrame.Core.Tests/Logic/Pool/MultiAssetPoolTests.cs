using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Logic.Pool;
using NUnit.Framework.Constraints;

namespace ImmichFrame.Core.Tests.Logic.Pool
{
    [TestFixture]
    public class MultiAssetPoolTests
    {
        private Mock<IAssetPool> _mockPool1;
        private Mock<IAssetPool> _mockPool2;
        private Mock<IAssetPool> _mockPool3;
        private MultiAssetPool _multiPool;

        // Hands out up to n queued assets, like a real pool answering a batch request.
        private static List<AssetResponseDto> Drain(Queue<AssetResponseDto> queue, int n)
        {
            var batch = new List<AssetResponseDto>();
            while (batch.Count < n && queue.Count > 0) batch.Add(queue.Dequeue());
            return batch;
        }

        [SetUp]
        public void Setup()
        {
            _mockPool1 = new Mock<IAssetPool>();
            _mockPool2 = new Mock<IAssetPool>();
            _mockPool3 = new Mock<IAssetPool>();
        }

        private AssetResponseDto CreateAsset(string id) => new AssetResponseDto { Id = FixtureHelpers.GuidFor(id), OriginalPath = $"/path/{id}.jpg", Type = AssetTypeEnum.IMAGE, ExifInfo = new ExifResponseDto() };

        [Test]
        public async Task GetAssetCount_NoPools_ReturnsZero()
        {
            _multiPool = new MultiAssetPool(new List<IAssetPool>());
            Assert.That(await _multiPool.GetAssetCount(CancellationToken.None), Is.EqualTo(0));
        }

        [Test]
        public async Task GetAssetCount_OnePool_ReturnsCorrectCount()
        {
            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(5L);
            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object });
            Assert.That(await _multiPool.GetAssetCount(CancellationToken.None), Is.EqualTo(5L));
        }

        [Test]
        public async Task GetAssetCount_MultiplePools_ReturnsSumOfCounts()
        {
            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(5L);
            _mockPool2.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(10L);
            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object, _mockPool2.Object });
            Assert.That(await _multiPool.GetAssetCount(CancellationToken.None), Is.EqualTo(15L));
        }

        [Test]
        public async Task GetAssets_RequestZeroAssets_ReturnsEmptyCollection()
        {
            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object });
            var result = await _multiPool.GetAssets(0, CancellationToken.None);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task GetAssets_TotalLessThanRequested_ReturnsAllAvailableAssets()
        {
            var asset1 = CreateAsset("a1");
            var asset2 = CreateAsset("a2");
            var pool1AvailableAssets = new Queue<AssetResponseDto>(new List<AssetResponseDto> { asset1, asset2 });
            var allAssetsFromPool1 = new List<AssetResponseDto> { asset1, asset2 };

            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => (long)pool1AvailableAssets.Count);

            _mockPool1.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => pool1AvailableAssets.Any()
                    ? new List<AssetResponseDto> { pool1AvailableAssets.Dequeue() }
                    : new List<AssetResponseDto>()); // Moq wraps this in Task.FromResult

            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object });

            var result = (await _multiPool.GetAssets(5, CancellationToken.None)).ToList();
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.All(x => allAssetsFromPool1.Contains(x)), Is.True);
            Assert.That(allAssetsFromPool1.All(x => result.Contains(x)), Is.True);
        }

        [Test]
        public async Task GetAssets_TotalMoreThanRequested_AggregatesAssets()
        {
            var p1a1 = CreateAsset("p1a1");
            var pool1Queue = new Queue<AssetResponseDto>(new[] { p1a1 });
            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(() => (long)pool1Queue.Count);
            _mockPool1.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int n, CancellationToken _) => Drain(pool1Queue, n));

            var p2a1 = CreateAsset("p2a1");
            var pool2Queue = new Queue<AssetResponseDto>(new[] { p2a1 });
            _mockPool2.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(() => (long)pool2Queue.Count);
            _mockPool2.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int n, CancellationToken _) => Drain(pool2Queue, n));

            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object, _mockPool2.Object });

            var result = (await _multiPool.GetAssets(2, CancellationToken.None)).ToList();
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.Contains(p1a1) && result.Contains(p2a1), Is.True);
        }

        [Test]
        public async Task GetAssets_PoolReturnsFewerAssetsThanCountSuggests_HandlesGracefully()
        {
            var p1a1 = CreateAsset("p1a1");
            var p1a2 = CreateAsset("p1a2");
            var pool1AvailableAssets = new Queue<AssetResponseDto>(new List<AssetResponseDto> { p1a1, p1a2 });
            var originalPool1Assets = new List<AssetResponseDto> { p1a1, p1a2 };

            var pool1ReportedCount = 5L;
            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => pool1ReportedCount);
            _mockPool1.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    if (pool1AvailableAssets.Any())
                    {
                        if (pool1AvailableAssets.Count == 1) pool1ReportedCount = 0L; // Update reported count after last actual asset
                        return new List<AssetResponseDto> { pool1AvailableAssets.Dequeue() };
                    }

                    pool1ReportedCount = 0L; // Ensure reported count is 0 if called after exhaustion
                    return new List<AssetResponseDto>();
                });

            var p2a1 = CreateAsset("p2a1");
            var pool2AvailableAssets = new Queue<AssetResponseDto>(new List<AssetResponseDto> { p2a1 });
            var originalPool2Assets = new List<AssetResponseDto> { p2a1 };
            _mockPool2.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => (long)pool2AvailableAssets.Count);
            _mockPool2.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int n, CancellationToken _) => Drain(pool2AvailableAssets, n));

            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object, _mockPool2.Object });
            var result = (await _multiPool.GetAssets(5, CancellationToken.None)).ToList();

            Assert.That(result.Count, Is.EqualTo(3));
            var expectedTotalAssets = originalPool1Assets.Concat(originalPool2Assets).ToList();
            Assert.That(result.All(x => expectedTotalAssets.Contains(x)), Is.True);
            Assert.That(expectedTotalAssets.All(x => result.Contains(x)), Is.True);
        }

        [Test]
        public async Task GetAssets_DifferentAssetCounts_RetrievesAssetsFromPools()
        {
            var p1a1 = CreateAsset("p1a1");
            var p1a2 = CreateAsset("p1a2");
            var p1a3 = CreateAsset("p1a3");
            var pool1Queue = new Queue<AssetResponseDto>(new List<AssetResponseDto> { p1a1, p1a2, p1a3 });
            var originalPool1Assets = new List<AssetResponseDto> { p1a1, p1a2, p1a3 };

            var p2a1 = CreateAsset("p2a1");
            var pool2Queue = new Queue<AssetResponseDto>(new List<AssetResponseDto> { p2a1 });
            var originalPool2Assets = new List<AssetResponseDto> { p2a1 };

            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => (long)pool1Queue.Count);
            _mockPool1.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int n, CancellationToken _) => Drain(pool1Queue, n));

            _mockPool2.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => (long)pool2Queue.Count);
            _mockPool2.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int n, CancellationToken _) => Drain(pool2Queue, n));

            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object, _mockPool2.Object });

            var retrievedAssets = new List<AssetResponseDto>();
            for (int i = 0; i < 5; i++)
            {
                var assetResultList = await _multiPool.GetAssets(1, CancellationToken.None);
                var assetResult = assetResultList.FirstOrDefault();
                if (assetResult != null)
                {
                    retrievedAssets.Add(assetResult);
                }
                else if (retrievedAssets.Count >= 4)
                {
                    break;
                }
            }

            Assert.That(retrievedAssets.Count, Is.EqualTo(4));
            Assert.That(retrievedAssets.Count(a => originalPool1Assets.Contains(a)), Is.EqualTo(3));
            Assert.That(retrievedAssets.Count(a => originalPool2Assets.Contains(a)), Is.EqualTo(1));
        }

        [Test]
        public async Task GetAssets_PoolWithZeroCount_IsNotCalledForAssets()
        {
            var assets1 = new List<AssetResponseDto> { CreateAsset("p1a1") };
            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(1L);
            _mockPool1.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(assets1);

            _mockPool2.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(0L);
            _mockPool2.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<AssetResponseDto>());

            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object, _mockPool2.Object });
            var result = (await _multiPool.GetAssets(1, CancellationToken.None)).ToList();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.First(), Is.SameAs(assets1.First()));
        }

        [Test]
        public async Task GetNextAssetBehavior_NoAssetsInAnyPool_ReturnsNull()
        {
            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(0L);
            _mockPool2.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(0L);
            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object, _mockPool2.Object });

            var result = (await _multiPool.GetAssets(1, CancellationToken.None)).FirstOrDefault();
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetNextAssetBehavior_RetrievesAllAssets()
        {
            var assetP1A1 = CreateAsset("P1A1");
            var assetP2A1 = CreateAsset("P2A1");
            var q1 = new Queue<AssetResponseDto>(new[] { assetP1A1 });
            var q2 = new Queue<AssetResponseDto>(new[] { assetP2A1 });

            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(() => (long)q1.Count);
            _mockPool1.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((int n, CancellationToken _) => Drain(q1, n));

            _mockPool2.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(() => (long)q2.Count);
            _mockPool2.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((int n, CancellationToken _) => Drain(q2, n));

            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object, _mockPool2.Object });

            var results = new HashSet<AssetResponseDto>();
            var asset1 = (await _multiPool.GetAssets(1, CancellationToken.None)).FirstOrDefault();
            if (asset1 != null) results.Add(asset1);

            var asset2 = (await _multiPool.GetAssets(1, CancellationToken.None)).FirstOrDefault();
            if (asset2 != null) results.Add(asset2);

            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results, Does.Contain(assetP1A1));
            Assert.That(results, Does.Contain(assetP2A1));

            var asset3 = (await _multiPool.GetAssets(1, CancellationToken.None)).FirstOrDefault();
            Assert.That(asset3, Is.Null);
        }

        [Test]
        public async Task GetNextAssetBehavior_PoolWithZeroCount_IsNotCalled()
        {
            var asset1 = CreateAsset("p1a1");
            var q1 = new Queue<AssetResponseDto>(new[] { asset1 });
            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(() => (long)q1.Count);
            _mockPool1.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((int n, CancellationToken _) => Drain(q1, n));

            _mockPool2.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(0L);
            _mockPool2.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<AssetResponseDto>());

            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object, _mockPool2.Object });

            var result = (await _multiPool.GetAssets(1, CancellationToken.None)).FirstOrDefault();
            Assert.That(result, Is.SameAs(asset1));

            _mockPool1.Verify(p => p.GetAssets(1, It.IsAny<CancellationToken>()), Times.Once());
            _mockPool2.Verify(p => p.GetAssets(1, It.IsAny<CancellationToken>()), Times.Never());

            var nextResult = (await _multiPool.GetAssets(1, CancellationToken.None)).FirstOrDefault();
            Assert.That(nextResult, Is.Null, "Pool1 exhausted, Pool2 zero count, should be null");
        }

        [Test]
        public async Task GetNextAssetBehavior_PoolIsExhausted_SwitchesToNextAvailablePool()
        {
            var assetP2A1 = CreateAsset("P2A1");
            var assetP3A1 = CreateAsset("P3A1");
            var q2 = new Queue<AssetResponseDto>(new[] { assetP2A1 });
            var q3 = new Queue<AssetResponseDto>(new[] { assetP3A1 });

            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(0L);
            _mockPool2.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(() => (long)q2.Count);
            _mockPool2.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((int n, CancellationToken _) => Drain(q2, n));
            _mockPool3.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(() => (long)q3.Count);
            _mockPool3.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((int n, CancellationToken _) => Drain(q3, n));

            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object, _mockPool2.Object, _mockPool3.Object });

            var result1 = (await _multiPool.GetAssets(1, CancellationToken.None)).FirstOrDefault();
            Assert.That(result1, Is.EqualTo(assetP2A1).Or.EqualTo(assetP3A1));
            var result2 = (await _multiPool.GetAssets(1, CancellationToken.None)).FirstOrDefault();
            if (result1 == assetP2A1)
                Assert.That(result2, Is.SameAs(assetP3A1), "Should get asset from Pool 3 if Pool 2 was first");
            else
                Assert.That(result2, Is.SameAs(assetP2A1), "Should get asset from Pool 2 if Pool 3 was first");
        }

        [Test]
        public async Task GetAssets_Batch_CallsEachPoolOnce()
        {
            // Regression: picking one asset at a time meant one Immich round-trip per asset
            // (25 sequential random searches per batch with server-side search pools).
            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(1000L);
            _mockPool1.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int n, CancellationToken _) => Enumerable.Range(0, n).Select(i => CreateAsset($"p1-{Guid.NewGuid()}")).ToList());
            _mockPool2.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(1000L);
            _mockPool2.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int n, CancellationToken _) => Enumerable.Range(0, n).Select(i => CreateAsset($"p2-{Guid.NewGuid()}")).ToList());
            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object, _mockPool2.Object });

            var result = (await _multiPool.GetAssets(25, CancellationToken.None)).ToList();

            Assert.That(result, Has.Count.EqualTo(25));
            _mockPool1.Verify(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtMostOnce());
            _mockPool2.Verify(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtMostOnce());
        }

        [Test]
        public async Task GetAssets_Batch_SplitsProportionallyToPoolSize()
        {
            var requestedFrom = new int[2];
            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(900L);
            _mockPool1.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int n, CancellationToken _) => { requestedFrom[0] += n; return Enumerable.Range(0, n).Select(i => CreateAsset($"a{Guid.NewGuid()}")).ToList(); });
            _mockPool2.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(100L);
            _mockPool2.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int n, CancellationToken _) => { requestedFrom[1] += n; return Enumerable.Range(0, n).Select(i => CreateAsset($"b{Guid.NewGuid()}")).ToList(); });
            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object, _mockPool2.Object });

            await _multiPool.GetAssets(1000, CancellationToken.None);

            // Expected 900/100; binomial sd ~9.5, so +/-60 is > 6 sd.
            Assert.That(requestedFrom[0], Is.InRange(840, 960));
            Assert.That(requestedFrom[0] + requestedFrom[1], Is.EqualTo(1000));
        }

        [Test]
        public async Task GetAssets_Batch_ShortfallIsFilledFromOtherPools()
        {
            // Pool 1 claims many assets but only has 2; pool 2 has plenty.
            var pool1 = new Queue<AssetResponseDto>(new[] { CreateAsset("s1"), CreateAsset("s2") });
            var pool1Count = 1000L;
            _mockPool1.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(() => pool1Count);
            _mockPool1.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int n, CancellationToken _) => { var batch = Drain(pool1, n); pool1Count = pool1.Count; return batch; });
            _mockPool2.Setup(p => p.GetAssetCount(It.IsAny<CancellationToken>())).ReturnsAsync(10L);
            _mockPool2.Setup(p => p.GetAssets(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int n, CancellationToken _) => Enumerable.Range(0, n).Select(i => CreateAsset($"big{Guid.NewGuid()}")).ToList());
            _multiPool = new MultiAssetPool(new List<IAssetPool> { _mockPool1.Object, _mockPool2.Object });

            var result = (await _multiPool.GetAssets(10, CancellationToken.None)).ToList();

            Assert.That(result, Has.Count.EqualTo(10));
        }
    }
}
