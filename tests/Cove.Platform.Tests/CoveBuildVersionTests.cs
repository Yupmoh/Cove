using System.Text.RegularExpressions;
using Cove.Platform;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class CoveBuildVersionTests
{
    private static readonly Regex SemanticVersion = new(
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$",
        RegexOptions.CultureInvariant);

    [Fact]
    public void InformationalVersion_IsConcreteSemanticVersion()
    {
        var version = CoveBuild.InformationalVersion;

        Assert.Matches(SemanticVersion, version);
        Assert.DoesNotContain("$(", version, StringComparison.Ordinal);
        Assert.DoesNotContain("unknown", version, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("placeholder", version, StringComparison.OrdinalIgnoreCase);
    }
}
