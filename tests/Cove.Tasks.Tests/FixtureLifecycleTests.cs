using Cove.Testing;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class FixtureLifecycleTests
{
    [Fact]
    public async System.Threading.Tasks.Task DisposeAsync_BlockedResourceTimesOut_ContinuesAndAggregatesCleanupFailures()
    {
        var release = new System.Threading.Tasks.TaskCompletionSource(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupFailure = new InvalidOperationException("later cleanup failed");
        var fixture = new FixtureProbe(System.TimeSpan.FromMilliseconds(100));
        fixture.Own(new ThrowingAsyncDisposable(cleanupFailure));
        fixture.Own(new BlockingAsyncDisposable(release.Task));

        var disposal = fixture.DisposeAsync();
        var aggregate = await Assert.ThrowsAsync<AggregateException>(
            () => AsyncTest.CompletesWithinAsync(
                disposal,
                System.TimeSpan.FromSeconds(2),
                "fixture disposal exceeded the test deadline"));

        release.TrySetResult();
        var failures = aggregate.Flatten().InnerExceptions;
        Assert.Contains(failures, failure =>
            failure is TimeoutException &&
            failure.Message == "Tasks test resource disposal did not complete");
        Assert.Contains(cleanupFailure, failures);
    }

    private sealed class FixtureProbe : TasksTestBase
    {
        public FixtureProbe(System.TimeSpan lifecycleTimeout)
            : base(lifecycleTimeout)
        {
        }

        public void Own(IAsyncDisposable resource) => TrackResource(resource);
    }

    private sealed class BlockingAsyncDisposable(System.Threading.Tasks.Task disposal) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => new(disposal);
    }

    private sealed class ThrowingAsyncDisposable(Exception exception) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.FromException(exception);
    }
}
