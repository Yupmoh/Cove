using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class BundledAdapterSeederTests : IDisposable
{
    private readonly string _root;

    public BundledAdapterSeederTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "cove-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private string MakeSourceAdapter(string sourceRoot, string name, string manifestBody)
    {
        var dir = Path.Combine(sourceRoot, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "adapter.json"), manifestBody);
        File.WriteAllText(Path.Combine(dir, "build_launch_command.sh"), "#!/bin/sh\necho " + name);
        return dir;
    }

    [Fact]
    public void ResolveSourceDir_WalksUpToRepoAdaptersLayout()
    {
        var repo = Path.Combine(_root, "repo");
        var adapters = Path.Combine(repo, "adapters");
        MakeSourceAdapter(adapters, "claude-code", "{\"name\":\"claude-code\"}");
        var binDir = Path.Combine(repo, "src", "Cove.Cli", "bin", "Debug", "net10.0");
        Directory.CreateDirectory(binDir);

        var resolved = BundledAdapterSeeder.ResolveSourceDir(binDir);

        Assert.Equal(Path.GetFullPath(adapters), Path.GetFullPath(resolved!));
    }

    [Fact]
    public void ResolveSourceDir_PrefersBaseDirectoryAdapters()
    {
        var baseDir = Path.Combine(_root, "publish");
        var adapters = Path.Combine(baseDir, "adapters");
        MakeSourceAdapter(adapters, "claude-code", "{\"name\":\"claude-code\"}");

        var resolved = BundledAdapterSeeder.ResolveSourceDir(baseDir);

        Assert.Equal(Path.GetFullPath(adapters), Path.GetFullPath(resolved!));
    }

    [Fact]
    public void ResolveSourceDir_ReturnsNullWhenNoBundledAdaptersFound()
    {
        var baseDir = Path.Combine(_root, "empty", "deep", "nested");
        Directory.CreateDirectory(baseDir);

        Assert.Null(BundledAdapterSeeder.ResolveSourceDir(baseDir));
    }

    [Fact]
    public void ComputeDirHash_IsStableForIdenticalContent()
    {
        var a = MakeSourceAdapter(Path.Combine(_root, "srcA"), "codex", "{\"name\":\"codex\"}");
        var b = MakeSourceAdapter(Path.Combine(_root, "srcB"), "codex", "{\"name\":\"codex\"}");

        Assert.Equal(BundledAdapterSeeder.ComputeDirHash(a), BundledAdapterSeeder.ComputeDirHash(b));
    }

    [Fact]
    public void ComputeDirHash_ChangesWhenContentChanges()
    {
        var dir = MakeSourceAdapter(Path.Combine(_root, "srcC"), "codex", "{\"name\":\"codex\"}");
        var before = BundledAdapterSeeder.ComputeDirHash(dir);
        File.WriteAllText(Path.Combine(dir, "adapter.json"), "{\"name\":\"codex\",\"binary\":\"codex\"}");

        Assert.NotEqual(before, BundledAdapterSeeder.ComputeDirHash(dir));
    }

    [Fact]
    public void ComputeDirHash_IgnoresStampFile()
    {
        var dir = MakeSourceAdapter(Path.Combine(_root, "srcD"), "codex", "{\"name\":\"codex\"}");
        var before = BundledAdapterSeeder.ComputeDirHash(dir);
        File.WriteAllText(Path.Combine(dir, BundledAdapterSeeder.StampFileName), "deadbeef");

        Assert.Equal(before, BundledAdapterSeeder.ComputeDirHash(dir));
    }

    [Fact]
    public void Seed_CopiesAbsentAdapterAndWritesStamp()
    {
        var source = Path.Combine(_root, "source");
        MakeSourceAdapter(source, "claude-code", "{\"name\":\"claude-code\"}");
        var target = Path.Combine(_root, "target");

        var report = BundledAdapterSeeder.Seed(source, target);

        Assert.Contains("claude-code", report.Copied);
        Assert.True(File.Exists(Path.Combine(target, "claude-code", "adapter.json")));
        var stamp = Path.Combine(target, "claude-code", BundledAdapterSeeder.StampFileName);
        Assert.True(File.Exists(stamp));
        Assert.Equal(BundledAdapterSeeder.ComputeDirHash(Path.Combine(source, "claude-code")), File.ReadAllText(stamp).Trim());
    }

    [Fact]
    public void Seed_RefreshesStampedAdapterWhenSourceChanged()
    {
        var source = Path.Combine(_root, "source");
        MakeSourceAdapter(source, "codex", "{\"name\":\"codex\"}");
        var target = Path.Combine(_root, "target");
        BundledAdapterSeeder.Seed(source, target);

        File.WriteAllText(Path.Combine(source, "codex", "adapter.json"), "{\"name\":\"codex\",\"binary\":\"codex\"}");
        var report = BundledAdapterSeeder.Seed(source, target);

        Assert.Contains("codex", report.Refreshed);
        Assert.Contains("binary", File.ReadAllText(Path.Combine(target, "codex", "adapter.json")));
    }

    [Fact]
    public void Seed_LeavesStampedAdapterUntouchedWhenSourceUnchanged()
    {
        var source = Path.Combine(_root, "source");
        MakeSourceAdapter(source, "omp", "{\"name\":\"omp\"}");
        var target = Path.Combine(_root, "target");
        BundledAdapterSeeder.Seed(source, target);
        var marker = Path.Combine(target, "omp", "marker.txt");
        File.WriteAllText(marker, "keep");

        var report = BundledAdapterSeeder.Seed(source, target);

        Assert.DoesNotContain("omp", report.Copied);
        Assert.DoesNotContain("omp", report.Refreshed);
        Assert.True(File.Exists(marker));
    }

    [Fact]
    public void Seed_LeavesUnstampedTargetUntouched()
    {
        var source = Path.Combine(_root, "source");
        MakeSourceAdapter(source, "claude-code", "{\"name\":\"claude-code\"}");
        var target = Path.Combine(_root, "target");
        var userDir = Path.Combine(target, "claude-code");
        Directory.CreateDirectory(userDir);
        File.WriteAllText(Path.Combine(userDir, "adapter.json"), "{\"name\":\"claude-code\",\"user\":true}");

        var report = BundledAdapterSeeder.Seed(source, target);

        Assert.Contains("claude-code", report.SkippedUserManaged);
        Assert.False(File.Exists(Path.Combine(userDir, BundledAdapterSeeder.StampFileName)));
        Assert.Contains("user", File.ReadAllText(Path.Combine(userDir, "adapter.json")));
    }

    [Fact]
    public void Seed_NeverTouchesForeignAdapterDirs()
    {
        var source = Path.Combine(_root, "source");
        MakeSourceAdapter(source, "codex", "{\"name\":\"codex\"}");
        var target = Path.Combine(_root, "target");
        var foreign = Path.Combine(target, "my-custom-harness");
        Directory.CreateDirectory(foreign);
        File.WriteAllText(Path.Combine(foreign, "adapter.json"), "{\"name\":\"my-custom-harness\"}");

        BundledAdapterSeeder.Seed(source, target);

        Assert.True(File.Exists(Path.Combine(foreign, "adapter.json")));
        Assert.False(File.Exists(Path.Combine(foreign, BundledAdapterSeeder.StampFileName)));
        Assert.False(Directory.Exists(Path.Combine(source, "my-custom-harness")));
    }

    [Fact]
    public void Seed_SkipsSourceSubdirsWithoutManifest()
    {
        var source = Path.Combine(_root, "source");
        MakeSourceAdapter(source, "codex", "{\"name\":\"codex\"}");
        Directory.CreateDirectory(Path.Combine(source, "registry-cache"));
        File.WriteAllText(Path.Combine(source, "registry-cache", "data.json"), "{}");
        var target = Path.Combine(_root, "target");

        var report = BundledAdapterSeeder.Seed(source, target);

        Assert.Contains("codex", report.Copied);
        Assert.DoesNotContain("registry-cache", report.Copied);
        Assert.False(Directory.Exists(Path.Combine(target, "registry-cache")));
    }

    [Fact]
    public void Seed_ReturnsEmptyReportWhenSourceMissing()
    {
        var report = BundledAdapterSeeder.Seed(Path.Combine(_root, "nope"), Path.Combine(_root, "target"));

        Assert.Empty(report.Copied);
        Assert.Empty(report.Refreshed);
        Assert.Empty(report.SkippedUserManaged);
    }
}
