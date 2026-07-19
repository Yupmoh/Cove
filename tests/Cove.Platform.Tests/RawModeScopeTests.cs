using Cove.Platform.Terminal;
using Cove.Testing;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class RawModeScopeTests
{
    [Fact]
    public void Dispose_CalledTwice_RestoresOnce()
    {
        var restoreCount = 0;
        var scope = new TestRawModeScope(() => restoreCount++);

        scope.Dispose();
        scope.Dispose();

        Assert.Equal(1, restoreCount);
    }

    [Fact]
    public async Task DisposeAsync_AfterDispose_DoesNotRestoreAgain()
    {
        var restoreCount = 0;
        var scope = new TestRawModeScope(() => restoreCount++);

        scope.Dispose();
        await scope.DisposeAsync();

        Assert.Equal(1, restoreCount);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void TryEnter_InvalidFileDescriptor_ReturnsNull()
    {
        var scope = RawModeScope.TryEnter(-1);

        Assert.Null(scope);
    }

    private sealed class TestRawModeScope(Action restore) : RawModeScope
    {
        protected override void Restore() => restore();
    }
}
