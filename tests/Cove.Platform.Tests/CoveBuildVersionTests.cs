using Cove.Platform;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class CoveBuildVersionTests
{
    [Fact]
    public void InformationalVersion_IsNonEmptyDottedVersion()
    {
        var version = CoveBuild.InformationalVersion;

        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.Contains('.', version);
    }
}
