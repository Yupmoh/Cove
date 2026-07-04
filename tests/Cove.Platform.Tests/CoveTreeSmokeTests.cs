using System;
using System.IO;
using Cove.Platform;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class CoveTreeSmokeTests
{
    [Fact]
    public void Ensure_AtAmbientCoveDataDir_ForShellSmoke()
    {
        var root = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        if (string.IsNullOrWhiteSpace(root))
            return;
        var dd = CoveDataDir.Resolve(CoveChannel.Stable);
        CoveTree.Ensure(dd);
        Assert.True(Directory.Exists(dd.Root));
    }
}
