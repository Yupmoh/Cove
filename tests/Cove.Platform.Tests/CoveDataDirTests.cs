using System;
using System.IO;
using Cove.Platform;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class CoveDataDirTests
{
    private static string HomeLikeCode() =>
        OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : Environment.GetEnvironmentVariable("HOME")
              ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [Fact]
    public void Resolve_WithOverride_UsesFullPath()
    {
        var target = Path.Combine(Path.GetTempPath(), "cove-ovr-" + Guid.NewGuid().ToString("N"), ".cove");
        var prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("COVE_DATA_DIR", target);
            var dd = CoveDataDir.Resolve(CoveChannel.Stable);
            Assert.Equal(Path.GetFullPath(target), dd.Root);
        }
        finally { Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev); }
    }

    [Fact]
    public void Resolve_OverrideExpandsLeadingTilde()
    {
        var sub = "cove-tilde-" + Guid.NewGuid().ToString("N");
        var prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("COVE_DATA_DIR", "~/" + sub);
            var dd = CoveDataDir.Resolve(CoveChannel.Stable);
            var expected = Path.GetFullPath(Path.Combine(HomeLikeCode(), sub));
            Assert.Equal(expected, dd.Root);
        }
        finally { Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev); }
    }

    [Fact]
    public void Resolve_StableChannel_DirIsDotCove()
    {
        var prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("COVE_DATA_DIR", null);
            var dd = CoveDataDir.Resolve(CoveChannel.Stable);
            Assert.Equal(".cove", Path.GetFileName(dd.Root));
            Assert.True(Path.IsPathRooted(dd.Root));
        }
        finally { Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev); }
    }

    [Fact]
    public void Resolve_BetaChannel_DirIsDotCoveBeta()
    {
        var prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("COVE_DATA_DIR", null);
            var dd = CoveDataDir.Resolve(CoveChannel.Beta);
            Assert.Equal(".cove-beta", Path.GetFileName(dd.Root));
        }
        finally { Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev); }
    }

    [Fact]
    public void Resolve_DevChannel_DirIsDotCoveDev()
    {
        var prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("COVE_DATA_DIR", null);
            var dd = CoveDataDir.Resolve(CoveChannel.Dev);
            Assert.Equal(".cove-dev", Path.GetFileName(dd.Root));
        }
        finally { Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev); }
    }

    [Fact]
    public void PathProperties_AreComposedUnderRoot()
    {
        var target = Path.Combine(Path.GetTempPath(), "cove-paths-" + Guid.NewGuid().ToString("N"), ".cove");
        var prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("COVE_DATA_DIR", target);
            var dd = CoveDataDir.Resolve(CoveChannel.Stable);
            var root = dd.Root;
            Assert.Equal(Path.Combine(root, "ipc"), dd.IpcDir);
            Assert.Equal(Path.Combine(root, "ipc", "stable.sock"), dd.SocketPath);
            Assert.Equal(Path.Combine(root, "hook-port"), dd.HookPortFile);
            Assert.Equal(Path.Combine(root, "logs"), dd.LogsDir);
            Assert.Equal(Path.Combine(root, "bin"), dd.BinDir);
            Assert.Equal(Path.Combine(root, "cache"), dd.CacheDir);
            Assert.Equal(Path.Combine(root, "workspaces"), dd.WorkspacesDir);
            Assert.Equal(Path.Combine(root, "themes"), dd.ThemesDir);
            Assert.Equal(Path.Combine(root, "library"), dd.LibraryDir);
            Assert.Equal(Path.Combine(root, "run-commands"), dd.RunCommandsDir);
            Assert.Equal(Path.Combine(root, "skills"), dd.SkillsDir);
            Assert.Equal(Path.Combine(root, "config.json"), dd.ConfigJson);
            Assert.Equal(Path.Combine(root, "state.json"), dd.StateJson);
            Assert.Equal(Path.Combine(root, ".cove-meta.json"), dd.MetaJson);
            Assert.Equal(Path.Combine(root, ".gitignore"), dd.GitIgnore);
        }
        finally { Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev); }
    }

    [Fact]
    public void SocketPath_UsesChannelName()
    {
        var target = Path.Combine(Path.GetTempPath(), "cove-sock-" + Guid.NewGuid().ToString("N"), ".cove");
        var prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("COVE_DATA_DIR", target);
            var beta = CoveDataDir.Resolve(CoveChannel.Beta);
            var dev = CoveDataDir.Resolve(CoveChannel.Dev);
            Assert.Equal(Path.Combine(beta.Root, "ipc", "beta.sock"), beta.SocketPath);
            Assert.Equal(Path.Combine(dev.Root, "ipc", "dev.sock"), dev.SocketPath);
        }
        finally { Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev); }
    }
}
