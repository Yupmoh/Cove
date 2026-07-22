using Cove.Engine.Pty;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CwdResolveTests
{
    [Fact]
    public void Resolve_ExistingInheritedWinsWithoutInspectingMissingExplicit()
    {
        var inherited = NewDirectory();
        var missingExplicit = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(
                inherited,
                NookRegistry.ResolveWorkingDirectory(
                    inherited,
                    missingExplicit,
                    null,
                    Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        }
        finally
        {
            Directory.Delete(inherited);
        }
    }

    [Fact]
    public void Resolve_MissingInheritedFallsToExistingExplicit()
    {
        var explicitCwd = NewDirectory();
        try
        {
            Assert.Equal(
                explicitCwd,
                NookRegistry.ResolveWorkingDirectory(
                    Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                    explicitCwd,
                    null,
                    Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        }
        finally
        {
            Directory.Delete(explicitCwd);
        }
    }

    [Fact]
    public void Resolve_MissingSelectedExplicitThrowsTypedFailure()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var failure = Assert.Throws<WorkingDirectoryException>(() =>
            NookRegistry.ResolveWorkingDirectory(null, missing, null, NewDirectory()));

        Assert.Equal(missing, failure.Path);
    }

    [Fact]
    public void Resolve_MissingProjectFallsToExistingHome()
    {
        var home = NewDirectory();
        try
        {
            Assert.Equal(
                home,
                NookRegistry.ResolveWorkingDirectory(
                    null,
                    null,
                    Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                    home));
        }
        finally
        {
            Directory.Delete(home);
        }
    }

    [Fact]
    public void Resolve_MissingHomeThrowsBeforeSpawn()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var failure = Assert.Throws<WorkingDirectoryException>(() =>
            NookRegistry.ResolveWorkingDirectory(null, null, null, missing));

        Assert.Equal(missing, failure.Path);
    }

    private static string NewDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "cove-cwd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
