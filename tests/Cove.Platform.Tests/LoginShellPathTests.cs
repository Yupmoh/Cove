using Cove.Platform;
using Cove.Testing;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class LoginShellPathTests
{
    [PlatformFact]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void Probe_ReturnsNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(LoginShellPath.Probe()));
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void Probe_OnUnix_ContainsStandardBin()
    {
        var p = LoginShellPath.Probe();
        Assert.True(p.Contains("/usr/bin") || p.Contains("/bin"));
    }

    [PlatformFact]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void Probe_DoesNotThrow()
    {
        var _ = Record.Exception(() => LoginShellPath.Probe());
        Assert.Null(_);
    }
}
