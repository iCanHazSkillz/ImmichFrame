using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic.AccountSelection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic.AccountSelection;

[TestFixture]
public class BloomFilterAssetAccountTrackerTests
{
    [Test]
    public async Task RecordAssetLocation_CreatesOneFilterForConcurrentSameAccountCalls()
    {
        var getTotalAssetsCalls = 0;
        var account = new Mock<IAccountImmichFrameLogic>();
        account
            .Setup(logic => logic.GetTotalAssets())
            .Returns(async () =>
            {
                Interlocked.Increment(ref getTotalAssetsCalls);
                await Task.Delay(50);
                return 100;
            });

        var accountLogic = account.Object;
        var tracker = new BloomFilterAssetAccountTracker(NullLogger<BloomFilterAssetAccountTracker>.Instance);
        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable
            .Range(0, 25)
            .Select(async index =>
            {
                await startGate.Task;
                await tracker.RecordAssetLocation(accountLogic, $"asset-{index}");
            })
            .ToList();

        startGate.SetResult();
        await Task.WhenAll(tasks);

        Assert.That(getTotalAssetsCalls, Is.EqualTo(1));
    }
}
