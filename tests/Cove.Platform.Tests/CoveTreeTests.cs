using System;
using System.IO;
using System.Text.Json;
using Cove.Platform;
using Cove.Testing;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class CoveTreeTests
{
    private static (CoveDataDir DataDir, string Parent) MakeHermeticRoot()
    {
        var parent = TestDirectory.Create("cove-tree-");
        var root = Path.Combine(parent, ".cove");
        return (CoveDataDir.ForRoot(CoveChannel.Stable, root), parent);
    }

    [Fact]
    public void Ensure_CreatesAllSkeletonDirs()
    {
        var (dd, parent) = MakeHermeticRoot();
        try
        {
            CoveTree.Ensure(dd);
            foreach (var name in new[] { "ipc", "logs", "bin", "cache", "bays", "themes", "library", "run-commands", "skills" })
                Assert.True(Directory.Exists(Path.Combine(dd.Root, name)), $"missing dir {name}");
        }
        finally { TestDirectory.Delete(parent); }
    }

    [Fact]
    public void Ensure_WritesGitIgnoreWithExpectedLines()
    {
        var (dd, parent) = MakeHermeticRoot();
        try
        {
            CoveTree.Ensure(dd);
            Assert.True(File.Exists(dd.GitIgnore));
            var text = File.ReadAllText(dd.GitIgnore);
            Assert.Equal(CoveGitIgnore.Content, text);
            Assert.Contains("*.db\n", text);
            Assert.Contains("ipc/\n", text);
        }
        finally { TestDirectory.Delete(parent); }
    }

    [Fact]
    public void Ensure_WritesTypedMetaFile()
    {
        var (dd, parent) = MakeHermeticRoot();
        try
        {
            var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            CoveTree.Ensure(dd);
            var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var meta = JsonSerializer.Deserialize(
                File.ReadAllText(dd.MetaJson),
                PlatformJsonContext.Default.DataDirMeta);

            Assert.NotNull(meta);
            Assert.Equal(DataDirMetaStore.CurrentSchemaVersion, meta.DataDirSchemaVersion);
            Assert.InRange(meta.CreatedAtUnixMs, before, after);
            Assert.Equal(CoveBuild.InformationalVersion, meta.CoveVersionAtCreate);
        }
        finally { TestDirectory.Delete(parent); }
    }

    [Fact]
    public void Ensure_IsIdempotent_MetaUnchanged()
    {
        var (dd, parent) = MakeHermeticRoot();
        try
        {
            CoveTree.Ensure(dd);
            var before = File.ReadAllText(dd.MetaJson);
            CoveTree.Ensure(dd);
            var after = File.ReadAllText(dd.MetaJson);
            Assert.Equal(before, after);
        }
        finally { TestDirectory.Delete(parent); }
    }

    [Fact]
    public void Ensure_DoesNotCreateLazyDirs()
    {
        var (dd, parent) = MakeHermeticRoot();
        try
        {
            CoveTree.Ensure(dd);
            Assert.False(Directory.Exists(dd.MemoryDir));
            Assert.False(Directory.Exists(dd.FtsDir));
            Assert.False(Directory.Exists(dd.NotesDir));
            Assert.False(Directory.Exists(dd.ReviewsDir));
            Assert.False(Directory.Exists(dd.AdaptersDir));
        }
        finally { TestDirectory.Delete(parent); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void Ensure_AppliesOwnerOnlyPerms_Posix()
    {
        var (dd, parent) = MakeHermeticRoot();
        try
        {
            CoveTree.Ensure(dd);
            Assert.False(OperatingSystem.IsWindows());
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                    File.GetUnixFileMode(dd.Root));
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                    File.GetUnixFileMode(dd.IpcDir));
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    File.GetUnixFileMode(dd.MetaJson));
            }
        }
        finally { TestDirectory.Delete(parent); }
    }
}
