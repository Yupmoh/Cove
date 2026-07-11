using System;
using System.IO;
using Cove.Platform;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class CoveTreeTests
{
    private static (CoveDataDir dd, string parent, string? prev) MakeHermeticRoot()
    {
        var parent = Path.Combine(Path.GetTempPath(), "cove-tree-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, ".cove");
        var prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        Environment.SetEnvironmentVariable("COVE_DATA_DIR", root);
        return (CoveDataDir.Resolve(CoveChannel.Stable), parent, prev);
    }

    private static void Cleanup(string parent, string? prev)
    {
        Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev);
        try { if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true); } catch { }
    }

    [Fact]
    public void Ensure_CreatesAllSkeletonDirs()
    {
        var (dd, parent, prev) = MakeHermeticRoot();
        try
        {
            CoveTree.Ensure(dd);
            foreach (var name in new[] { "ipc", "logs", "bin", "cache", "bays", "themes", "library", "run-commands", "skills" })
                Assert.True(Directory.Exists(Path.Combine(dd.Root, name)), $"missing dir {name}");
        }
        finally { Cleanup(parent, prev); }
    }

    [Fact]
    public void Ensure_WritesGitIgnoreWithExpectedLines()
    {
        var (dd, parent, prev) = MakeHermeticRoot();
        try
        {
            CoveTree.Ensure(dd);
            Assert.True(File.Exists(dd.GitIgnore));
            var text = File.ReadAllText(dd.GitIgnore);
            Assert.Equal(CoveGitIgnore.Content, text);
            Assert.Contains("*.db\n", text);
            Assert.Contains("ipc/\n", text);
        }
        finally { Cleanup(parent, prev); }
    }

    [Fact]
    public void Ensure_WritesMetaFile()
    {
        var (dd, parent, prev) = MakeHermeticRoot();
        try
        {
            CoveTree.Ensure(dd);
            Assert.True(File.Exists(dd.MetaJson));
            var text = File.ReadAllText(dd.MetaJson);
            Assert.Contains("dataDirSchemaVersion", text);
            Assert.Contains("createdAtUnixMs", text);
        }
        finally { Cleanup(parent, prev); }
    }

    [Fact]
    public void Ensure_IsIdempotent_MetaUnchanged()
    {
        var (dd, parent, prev) = MakeHermeticRoot();
        try
        {
            CoveTree.Ensure(dd);
            var before = File.ReadAllText(dd.MetaJson);
            CoveTree.Ensure(dd);
            var after = File.ReadAllText(dd.MetaJson);
            Assert.Equal(before, after);
        }
        finally { Cleanup(parent, prev); }
    }

    [Fact]
    public void Ensure_DoesNotCreateLazyDirs()
    {
        var (dd, parent, prev) = MakeHermeticRoot();
        try
        {
            CoveTree.Ensure(dd);
            Assert.False(Directory.Exists(dd.MemoryDir));
            Assert.False(Directory.Exists(dd.FtsDir));
            Assert.False(Directory.Exists(dd.NotesDir));
            Assert.False(Directory.Exists(dd.ReviewsDir));
            Assert.False(Directory.Exists(dd.AdaptersDir));
        }
        finally { Cleanup(parent, prev); }
    }

    [Fact]
    public void Ensure_AppliesOwnerOnlyPerms_Posix()
    {
        if (OperatingSystem.IsWindows())
            return;
        var (dd, parent, prev) = MakeHermeticRoot();
        try
        {
            CoveTree.Ensure(dd);
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
        finally { Cleanup(parent, prev); }
    }
}
