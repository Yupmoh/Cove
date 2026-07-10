using System.IO;
using Cove.Engine.Daemon;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CliBinLinkTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-bin-" + System.Guid.NewGuid().ToString("N"));

    [Fact]
    public void Ensure_CreatesLinkToProcessBinary()
    {
        var dataDir = NewDir();
        var srcDir = NewDir();
        try
        {
            Directory.CreateDirectory(srcDir);
            var source = Path.Combine(srcDir, "cove");
            File.WriteAllText(source, "binary");

            var linkPath = CliBinLink.Ensure(dataDir, source, NullLogger.Instance);

            Assert.Equal(Path.Combine(dataDir, "bin", "cove"), linkPath);
            Assert.True(File.Exists(linkPath));
            Assert.Equal("binary", File.ReadAllText(linkPath));
        }
        finally
        {
            try { Directory.Delete(dataDir, true); } catch { }
            try { Directory.Delete(srcDir, true); } catch { }
        }
    }

    [Fact]
    public void Ensure_IsIdempotentAndReplacesStaleLinks()
    {
        var dataDir = NewDir();
        var srcDir = NewDir();
        try
        {
            Directory.CreateDirectory(srcDir);
            var old = Path.Combine(srcDir, "cove-old");
            var current = Path.Combine(srcDir, "cove");
            File.WriteAllText(old, "old");
            File.WriteAllText(current, "current");

            CliBinLink.Ensure(dataDir, old, NullLogger.Instance);
            var linkPath = CliBinLink.Ensure(dataDir, current, NullLogger.Instance);
            var again = CliBinLink.Ensure(dataDir, current, NullLogger.Instance);

            Assert.Equal(linkPath, again);
            Assert.Equal("current", File.ReadAllText(linkPath));
        }
        finally
        {
            try { Directory.Delete(dataDir, true); } catch { }
            try { Directory.Delete(srcDir, true); } catch { }
        }
    }

    [Fact]
    public void Ensure_MissingSource_ReturnsPathWithoutThrowing()
    {
        var dataDir = NewDir();
        try
        {
            var linkPath = CliBinLink.Ensure(dataDir, Path.Combine(dataDir, "nope"), NullLogger.Instance);
            Assert.Equal(Path.Combine(dataDir, "bin", "cove"), linkPath);
        }
        finally { try { Directory.Delete(dataDir, true); } catch { } }
    }
}
