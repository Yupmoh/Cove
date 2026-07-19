using Cove.Dictation;
using Xunit;

namespace Cove.Dictation.Tests;

public sealed class PortAudioRecorderOwnershipTests
{
    private sealed class OwnedResource : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void StartOwned_DisposesResourceWhenStartFails()
    {
        var resource = new OwnedResource();

        var failure = Assert.Throws<InvalidOperationException>(
            () => PortAudioRecorder.StartOwned(
                () => resource,
                _ => throw new InvalidOperationException("start failed")));

        Assert.Equal("start failed", failure.Message);
        Assert.True(resource.Disposed);
    }

    [Fact]
    public void StartOwned_TransfersSuccessfulResourceToCaller()
    {
        var resource = new OwnedResource();

        var started = PortAudioRecorder.StartOwned(
            () => resource,
            _ => { });

        Assert.Same(resource, started);
        Assert.False(resource.Disposed);
    }
}
