using System.Text.Json;
using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class AdapterReloadWatcherTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-reload-" + Guid.NewGuid().ToString("N"));

    private static void WriteManifest(string dir, string name = "test-adapter", string version = "1.0.0")
    {
        Directory.CreateDirectory(dir);
        var manifest = $$"""
        {
          "name": "{{name}}",
          "displayName": "Test",
          "description": "test adapter",
          "accent": "#D97757",
          "binary": "test-cli",
          "sdkVersion": 2,
          "version": "{{version}}",
          "binaryDiscovery": {"commands": ["test-cli"], "wellKnownPaths": []},
          "methods": {
            "build_launch_command": {"script": "build_launch.sh"}
          }
        }
        """;
        File.WriteAllText(Path.Combine(dir, "adapter.json"), manifest);
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
                return true;
            await Task.Delay(50);
        }
        return condition();
    }

    [Fact]
    public async Task Reload_TriggersOnManifestChange()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        var adapterDir = Path.Combine(dir, "watched-adapter");
        WriteManifest(adapterDir);
        try
        {
            var changedName = (string?)null;
            using var watcher = new AdapterReloadWatcher(dir);
            watcher.AdapterChanged += (name) => changedName = name;
            watcher.Start();

            WriteManifest(adapterDir, version: "2.0.0");

            var ok = await WaitForAsync(() => changedName is not null, TimeSpan.FromSeconds(5));
            Assert.True(ok, "manifest change did not trigger reload within 5s");
            Assert.Equal("watched-adapter", changedName);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Reload_TriggersOnScriptChange()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        var adapterDir = Path.Combine(dir, "script-adapter");
        WriteManifest(adapterDir);
        File.WriteAllText(Path.Combine(adapterDir, "build_launch.sh"), "#!/usr/bin/env bash\necho v1\n");
        try
        {
            var triggered = false;
            using var watcher = new AdapterReloadWatcher(dir);
            watcher.AdapterChanged += (_) => triggered = true;
            watcher.Start();

            File.WriteAllText(Path.Combine(adapterDir, "build_launch.sh"), "#!/usr/bin/env bash\necho v2\n");

            var ok = await WaitForAsync(() => triggered, TimeSpan.FromSeconds(5));
            Assert.True(ok, "script change did not trigger reload within 5s");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Reload_TriggersOnAtomicRenameSwap()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        var adapterDir = Path.Combine(dir, "rename-adapter");
        WriteManifest(adapterDir);
        try
        {
            var changedName = (string?)null;
            using var watcher = new AdapterReloadWatcher(dir);
            watcher.AdapterChanged += (name) => changedName = name;
            watcher.Start();

            var tempDir = Path.Combine(dir, ".installing-rename-adapter");
            Directory.CreateDirectory(tempDir);
            WriteManifest(tempDir, "rename-adapter", version: "2.0.0");
            Directory.Delete(adapterDir, recursive: true);
            Directory.Move(tempDir, adapterDir);

            var ok = await WaitForAsync(() => changedName is not null, TimeSpan.FromSeconds(5));
            Assert.True(ok, "atomic rename swap did not trigger reload within 5s");
            Assert.Equal("rename-adapter", changedName);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Reload_DebouncesMultipleEvents()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        var adapterDir = Path.Combine(dir, "debounce-adapter");
        WriteManifest(adapterDir);
        try
        {
            var triggerCount = 0;
            using var watcher = new AdapterReloadWatcher(dir, debounceMs: 300);
            watcher.AdapterChanged += (_) => Interlocked.Increment(ref triggerCount);
            watcher.Start();

            for (int i = 0; i < 5; i++)
            {
                WriteManifest(adapterDir, version: $"1.0.{i}");
                await Task.Delay(10);
            }

            Assert.True(await WaitForAsync(() => Volatile.Read(ref triggerCount) >= 1, TimeSpan.FromSeconds(5)), "debounced reload never fired");
            await Task.Delay(700);
            var settled = Volatile.Read(ref triggerCount);
            Assert.True(settled <= 2, $"expected <=2 debounced events, got {settled}");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Reload_IgnoresTempInstallDirs()
    {
        Assert.False(AdapterReloadWatcher.IsWatchableDir(Path.Combine("x", ".installing-foo")));
        Assert.False(AdapterReloadWatcher.IsWatchableDir(Path.Combine("x", ".git")));
        Assert.True(AdapterReloadWatcher.IsWatchableDir(Path.Combine("x", "real-adapter")));
        Assert.False(AdapterReloadWatcher.IsWatchableDir(Path.Combine("x", ".DS_Store")));
    }

    [Fact]
    public async Task Reload_ThrowingHandler_DoesNotBreakSubsequentHandlers()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        var adapterDir = Path.Combine(dir, "resilient-adapter");
        WriteManifest(adapterDir);
        try
        {
            var secondFired = false;
            using var watcher = new AdapterReloadWatcher(dir);
            watcher.AdapterChanged += _ => throw new InvalidOperationException("boom");
            watcher.AdapterChanged += _ => secondFired = true;
            watcher.Start();

            WriteManifest(adapterDir, version: "2.0.0");

            var ok = await WaitForAsync(() => secondFired, TimeSpan.FromSeconds(5));
            Assert.True(ok, "second handler did not fire after first handler threw");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Reload_FiresAdaptersChangedAfterAdapterChanged()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        var adapterDir = Path.Combine(dir, "broadcast-adapter");
        WriteManifest(adapterDir);
        try
        {
            var adaptersChangedFired = false;
            using var watcher = new AdapterReloadWatcher(dir);
            watcher.AdaptersChanged += () => adaptersChangedFired = true;
            watcher.Start();

            WriteManifest(adapterDir, version: "2.0.0");

            var ok = await WaitForAsync(() => adaptersChangedFired, TimeSpan.FromSeconds(5));
            Assert.True(ok, "AdaptersChanged did not fire after manifest change");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
