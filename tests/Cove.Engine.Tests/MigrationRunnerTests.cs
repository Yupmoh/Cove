using System.IO;
using Cove.Engine.Migrations;
using Cove.Persistence;
using Cove.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace Cove.Engine.Tests;

public sealed class MigrationRunnerTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-mig-" + Guid.NewGuid().ToString("N"));

    private static void WriteMeta(string dir, int version)
    {
        Directory.CreateDirectory(dir);
        var meta = new DataDirMeta(version, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), "test");
        AtomicJsonStore.Write(Path.Combine(dir, "meta.json"), meta, PlatformJsonContext.Default.DataDirMeta);
    }

    [Fact]
    public void Migrate_NoMetaFile_InitializesToCurrentAndNoOps()
    {
        var dir = NewDir();
        try
        {
            var runner = new MigrationRunner(dir, NullLogger.Instance);
            var result = runner.Migrate();
            Assert.True(result.NoOp);
            Assert.Equal(DataDirMetaStore.CurrentSchemaVersion, result.ToVersion);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Migrate_CurrentVersion_NoOpEvenWithMigrations()
    {
        var dir = NewDir();
        try
        {
            WriteMeta(dir, DataDirMetaStore.CurrentSchemaVersion);
            var runner = new MigrationRunner(dir, NullLogger.Instance);
            runner.Register(DataDirMetaStore.CurrentSchemaVersion + 1, "v2", _ => { });
            var result = runner.Migrate();
            Assert.True(result.NoOp);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Migrate_BumpedVersion_RunsRegisteredMigration()
    {
        var dir = NewDir();
        try
        {
            WriteMeta(dir, 1);
            var runner = new MigrationRunner(dir, NullLogger.Instance, targetVersion: 2);
            var ran = false;
            runner.Register(2, "v2-add-flag", d => { ran = true; File.WriteAllText(Path.Combine(d, "migrated-v2"), "done"); });
            var result = runner.Migrate();
            Assert.False(result.NoOp);
            Assert.Equal(1, result.FromVersion);
            Assert.Equal(2, result.ToVersion);
            Assert.True(ran);
            Assert.True(File.Exists(Path.Combine(dir, "migrated-v2")));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Migrate_MultipleVersions_RunsSequentially()
    {
        var dir = NewDir();
        try
        {
            WriteMeta(dir, 1);
            var runner = new MigrationRunner(dir, NullLogger.Instance, targetVersion: 3);
            var order = new System.Collections.Generic.List<int>();
            runner.Register(2, "v2", _ => order.Add(2));
            runner.Register(3, "v3", _ => order.Add(3));
            var result = runner.Migrate();
            Assert.False(result.NoOp);
            Assert.Equal(3, result.ToVersion);
            Assert.Equal(new[] { 2, 3 }, order);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Migrate_UpdatesDataDirMetaVersion()
    {
        var dir = NewDir();
        try
        {
            WriteMeta(dir, 1);
            var runner = new MigrationRunner(dir, NullLogger.Instance, targetVersion: 2);
            runner.Register(2, "v2", _ => { });
            runner.Migrate();
            var runner2 = new MigrationRunner(dir, NullLogger.Instance);
            var result2 = runner2.Migrate();
            Assert.True(result2.NoOp);
            Assert.Equal(2, result2.FromVersion);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }
}
