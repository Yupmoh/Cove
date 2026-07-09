using System.IO;
using System.Linq;
using Cove.Engine.Search;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SearchRipgrepResolutionTests
{
    [Fact]
    public void BundledCandidates_IncludePerRidAndFlatPaths()
    {
        var exe = SearchService.RipgrepExecutableName();
        var candidates = SearchService.BundledRipgrepCandidates("/app", "osx-arm64").ToList();
        Assert.Contains(Path.Combine("/app", "tools", "rg", "osx-arm64", exe), candidates);
        Assert.Contains(Path.Combine("/app", exe), candidates);
    }

    [Fact]
    public void BundledCandidate_PrefersPerRidOverFlat()
    {
        var candidates = SearchService.BundledRipgrepCandidates("/app", "linux-x64").ToList();
        var perRid = candidates.FindIndex(c => c.Contains("linux-x64"));
        var flat = candidates.FindIndex(c => c == Path.Combine("/app", SearchService.RipgrepExecutableName()));
        Assert.True(perRid >= 0 && flat >= 0 && perRid < flat);
    }

    [Fact]
    public void ExecutableName_MatchesPlatform()
    {
        var expected = System.OperatingSystem.IsWindows() ? "rg.exe" : "rg";
        Assert.Equal(expected, SearchService.RipgrepExecutableName());
    }
}
