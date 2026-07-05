using Cove.Platform;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class LoginShellPathTests
{
    [Fact]
    public void Probe_ReturnsNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(LoginShellPath.Probe()));
    }

    [Fact]
    public void Probe_OnUnix_ContainsStandardBin()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        var p = LoginShellPath.Probe();
        Assert.True(p.Contains("/usr/bin") || p.Contains("/bin"));
    }

    [Fact]
    public void Probe_DoesNotThrow()
    {
        var _ = Record.Exception(() => LoginShellPath.Probe());
        Assert.Null(_);
    }
}
