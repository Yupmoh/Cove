using System.Text.Json;
using Cove.Adapters;
using Cove.Testing;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class AdapterInstallServiceTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-install-" + Guid.NewGuid().ToString("N"));

    private static void MakeExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
            System.IO.File.SetUnixFileMode(path, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);
    }

    private static string WriteManifest(string dir, string name = "test-adapter")
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
          "version": "1.0.0",
          "methods": {
            "build_launch_command": {"script": "build_launch.sh"},
            "build_resume_command": {"script": "build_resume.sh"},
            "list_recent_sessions": {"script": "list_recent_sessions.sh"}
          }
        }
        """;
        var path = Path.Combine(dir, "adapter.json");
        File.WriteAllText(path, manifest);
        return path;
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Install_FetchesFiles_SetsExecutableMode_AtomicRename_RunsHook()
    {
        var src = NewDir();
        var dest = NewDir();
        try
        {
            Directory.CreateDirectory(src);
            WriteManifest(src);
            foreach (var s in new[] { "build_launch.sh", "build_resume.sh", "list_recent_sessions.sh" })
            {
                var p = Path.Combine(src, s);
                File.WriteAllText(p, "#!/usr/bin/env bash\necho ok\n");
                MakeExecutable(p);
            }
            var scriptPath = Path.Combine(src, "hooks.sh");
            File.WriteAllText(scriptPath, "#!/usr/bin/env bash\nset -euo pipefail\necho \"$COVE_ADAPTER_DIR\" > \"$COVE_ADAPTER_DIR/installed.marker\"\n");
            MakeExecutable(scriptPath);
            var fetcher = new TestAdapterFetcher(src);
            var svc = new AdapterInstallService();

            var installed = await svc.InstallAsync(dest, "test-adapter", fetcher);

            Assert.True(Directory.Exists(Path.Combine(dest, "test-adapter")));
            Assert.False(Directory.Exists(Path.Combine(dest, ".installing-test-adapter")));
            Assert.True(File.Exists(Path.Combine(dest, "test-adapter", "adapter.json")));
            Assert.True(File.Exists(Path.Combine(dest, "test-adapter", "installed.marker")));
            var markerContent = await File.ReadAllTextAsync(Path.Combine(dest, "test-adapter", "installed.marker"));
            Assert.Equal(Path.Combine(dest, "test-adapter"), markerContent.Trim());
            var mode = System.IO.File.GetUnixFileMode(Path.Combine(dest, "test-adapter", "hooks.sh"));
            Assert.True((mode & System.IO.UnixFileMode.UserExecute) != 0);
            Assert.Equal("test-adapter", installed.Name);
        }
        finally { TestDirectory.Delete(src); TestDirectory.Delete(dest); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public async Task Install_InvalidManifest_RollsBackTempDir()
    {
        var src = NewDir();
        var dest = NewDir();
        try
        {
            Directory.CreateDirectory(src);
            File.WriteAllText(Path.Combine(src, "adapter.json"), """{"name":"bad","sdkVersion":99}""");
            var fetcher = new TestAdapterFetcher(src);
            var svc = new AdapterInstallService();

            await Assert.ThrowsAsync<AdapterInstallException>(() => svc.InstallAsync(dest, "bad", fetcher));

            Assert.False(Directory.Exists(Path.Combine(dest, "bad")));
            Assert.False(Directory.Exists(Path.Combine(dest, ".installing-bad")));
        }
        finally { TestDirectory.Delete(src); TestDirectory.Delete(dest); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task Uninstall_RunsHook_ThenDeletesDir()
    {
        var dest = NewDir();
        try
        {
            var adapterDir = Path.Combine(dest, "to-remove");
            Directory.CreateDirectory(adapterDir);
            WriteManifest(adapterDir, "to-remove");
            var scriptPath = Path.Combine(adapterDir, "hooks.sh");
            File.WriteAllText(scriptPath, "#!/usr/bin/env bash\nset -euo pipefail\necho uninstalled\n");
            MakeExecutable(scriptPath);
            var svc = new AdapterInstallService();

            await svc.UninstallAsync(dest, "to-remove");

            Assert.False(Directory.Exists(adapterDir));
        }
        finally { TestDirectory.Delete(dest); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task Uninstall_HookTimesOutAfterFiveSeconds_StillDeletesDir()
    {
        var dest = NewDir();
        try
        {
            var adapterDir = Path.Combine(dest, "slow-hook");
            Directory.CreateDirectory(adapterDir);
            WriteManifest(adapterDir, "slow-hook");
            var scriptPath = Path.Combine(adapterDir, "hooks.sh");
            File.WriteAllText(scriptPath, "#!/usr/bin/env bash\nsleep 30\n");
            MakeExecutable(scriptPath);
            var svc = new AdapterInstallService();

            await svc.UninstallAsync(dest, "slow-hook");

            Assert.False(Directory.Exists(adapterDir));
        }
        finally { TestDirectory.Delete(dest); }
    }
}

internal sealed class TestAdapterFetcher : IAdapterFetcher
{
    private readonly string _srcDir;
    public TestAdapterFetcher(string srcDir) => _srcDir = srcDir;

    public Task FetchIntoAsync(string destDir, CancellationToken ct = default)
    {
        foreach (var file in Directory.EnumerateFiles(_srcDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(_srcDir, file);
            var target = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
        return Task.CompletedTask;
    }
}
