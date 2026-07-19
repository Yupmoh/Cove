using System.IO;
using Cove.Engine.Daemon;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CliBinLinkTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-bin-" + System.Guid.NewGuid().ToString("N"));

    private static string ExpectedLink(string dataDir) =>
        Path.Combine(dataDir, "bin", OperatingSystem.IsWindows() ? "cove.exe" : "cove");

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

            Assert.Equal(ExpectedLink(dataDir), linkPath);
            Assert.True(File.Exists(linkPath));
            Assert.Equal("binary", File.ReadAllText(linkPath));
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(dataDir);
            Cove.Testing.TestDirectory.Delete(srcDir);
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
            Cove.Testing.TestDirectory.Delete(dataDir);
            Cove.Testing.TestDirectory.Delete(srcDir);
        }
    }

    [Fact]
    public void Ensure_MissingSource_ReturnsPathWithoutThrowing()
    {
        var dataDir = NewDir();
        try
        {
            var linkPath = CliBinLink.Ensure(dataDir, Path.Combine(dataDir, "nope"), NullLogger.Instance);
            Assert.Equal(ExpectedLink(dataDir), linkPath);
        }
        finally { Cove.Testing.TestDirectory.Delete(dataDir); }
    }
}
