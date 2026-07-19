using System;
using System.Collections.Generic;
using System.IO;
using Cove.Platform;
using Cove.Testing;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class CoveDataDirTests
{
    [PlatformFact]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public async Task Resolve_WithOverride_UsesFullPath()
    {
        var parent = TestDirectory.Create("cove-ovr-");
        var target = Path.Combine(parent, ".cove");
        try
        {
            await using var environment = await ProcessEnvironmentScope.SetAsync("COVE_DATA_DIR", target);
            var dd = CoveDataDir.Resolve(CoveChannel.Stable);
            Assert.Equal(Path.GetFullPath(target), dd.Root);
        }
        finally { TestDirectory.Delete(parent); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public async Task Resolve_OverrideExpandsLeadingTildeFromSyntheticHome()
    {
        var home = TestDirectory.Create("cove-home-");
        try
        {
            await using var environment = await ProcessEnvironmentScope.SetAsync(
                new Dictionary<string, string?>
                {
                    ["COVE_DATA_DIR"] = "~/nested/.cove",
                    ["HOME"] = home
                });
            var dd = CoveDataDir.Resolve(CoveChannel.Stable);
            Assert.Equal(Path.Combine(home, "nested", ".cove"), dd.Root);
        }
        finally { TestDirectory.Delete(home); }
    }

    [PlatformTheory(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    [InlineData(CoveChannel.Stable, ".cove")]
    [InlineData(CoveChannel.Beta, ".cove-beta")]
    [InlineData(CoveChannel.Dev, ".cove-dev")]
    public async Task Resolve_WithoutOverride_UsesSyntheticHomeAndChannelDirectory(
        CoveChannel channel,
        string directoryName)
    {
        var home = TestDirectory.Create("cove-home-");
        try
        {
            await using var environment = await ProcessEnvironmentScope.SetAsync(
                new Dictionary<string, string?>
                {
                    ["COVE_DATA_DIR"] = null,
                    ["HOME"] = home
                });
            var dd = CoveDataDir.Resolve(channel);
            Assert.Equal(Path.Combine(home, directoryName), dd.Root);
        }
        finally { TestDirectory.Delete(home); }
    }

    [Fact]
    public void PathProperties_AreComposedUnderRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-paths", ".cove");
        var dd = CoveDataDir.ForRoot(CoveChannel.Stable, root);

        Assert.Equal(Path.Combine(root, "ipc"), dd.IpcDir);
        Assert.Equal(Path.Combine(root, "ipc", "stable.sock"), dd.SocketPath);
        Assert.Equal(Path.Combine(root, "hook-port"), dd.HookPortFile);
        Assert.Equal(Path.Combine(root, "logs"), dd.LogsDir);
        Assert.Equal(Path.Combine(root, "bin"), dd.BinDir);
        Assert.Equal(Path.Combine(root, "cache"), dd.CacheDir);
        Assert.Equal(Path.Combine(root, "bays"), dd.BaysDir);
        Assert.Equal(Path.Combine(root, "themes"), dd.ThemesDir);
        Assert.Equal(Path.Combine(root, "library"), dd.LibraryDir);
        Assert.Equal(Path.Combine(root, "run-commands"), dd.RunCommandsDir);
        Assert.Equal(Path.Combine(root, "skills"), dd.SkillsDir);
        Assert.Equal(Path.Combine(root, "config.json"), dd.ConfigJson);
        Assert.Equal(Path.Combine(root, "state.json"), dd.StateJson);
        Assert.Equal(Path.Combine(root, ".cove-meta.json"), dd.MetaJson);
        Assert.Equal(Path.Combine(root, ".gitignore"), dd.GitIgnore);
    }

    [Fact]
    public void SocketPath_UsesChannelName()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-sock", ".cove");
        var beta = CoveDataDir.ForRoot(CoveChannel.Beta, root);
        var dev = CoveDataDir.ForRoot(CoveChannel.Dev, root);

        Assert.Equal(Path.Combine(root, "ipc", "beta.sock"), beta.SocketPath);
        Assert.Equal(Path.Combine(root, "ipc", "dev.sock"), dev.SocketPath);
    }
}
