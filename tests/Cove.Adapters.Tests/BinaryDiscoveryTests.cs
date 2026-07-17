using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class BinaryDiscoveryTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-disc-" + Guid.NewGuid().ToString("N"));

    private static string MakeExecutable(string dir, string name, string content)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "#!/usr/bin/env bash\n" + content);
        if (!OperatingSystem.IsWindows())
            System.IO.File.SetUnixFileMode(path, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);
        return path;
    }

    [Fact]
    public void Discover_FindsBinaryOnPath()
    {
        if (OperatingSystem.IsWindows()) return;
        var binDir = NewDir();
        try
        {
            MakeExecutable(binDir, "fake-cli", "echo '1.0.0'");
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            Environment.SetEnvironmentVariable("PATH", binDir + Path.PathSeparator + path);
            try
            {
                var discovery = new BinaryDiscoveryService();
                var result = discovery.Discover(new BinaryDiscovery { Commands = ["fake-cli"], VersionFlag = "--version" });

                Assert.Equal(AdapterDetectionState.Detected, result.State);
                Assert.NotNull(result.BinaryPath);
                Assert.Equal("1.0.0", result.Version);
            }
            finally { Environment.SetEnvironmentVariable("PATH", path); }
        }
        finally { try { Directory.Delete(binDir, true); } catch { } }
    }

    [Fact]
    public void Discover_FindsBinaryInWellKnownPath()
    {
        if (OperatingSystem.IsWindows()) return;
        var wkDir = NewDir();
        try
        {
            MakeExecutable(wkDir, "wk-cli", "echo '2.0.0'");
            var discovery = new BinaryDiscoveryService();
            var result = discovery.Discover(new BinaryDiscovery
            {
                Commands = ["wk-cli"],
                VersionFlag = "--version",
            }, wellKnownPaths: [wkDir]);

            Assert.Equal(AdapterDetectionState.Detected, result.State);
            Assert.Contains("wk-cli", result.BinaryPath ?? "");
        }
        finally { try { Directory.Delete(wkDir, true); } catch { } }
    }

    [Fact]
    public void Discover_MissingBinary_ReturnsMissingState()
    {
        var discovery = new BinaryDiscoveryService();
        var result = discovery.Discover(new BinaryDiscovery
        {
            Commands = ["definitely-not-a-real-binary-xyz"],
            VersionFlag = "--version",
        }, wellKnownPaths: []);

        Assert.Equal(AdapterDetectionState.Missing, result.State);
        Assert.Null(result.BinaryPath);
    }

    [Fact]
    public void Discover_BinaryWithNoVersion_ReturnsBrokenState()
    {
        if (OperatingSystem.IsWindows()) return;
        var binDir = NewDir();
        try
        {
            MakeExecutable(binDir, "no-version-cli", "exit 0");
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            Environment.SetEnvironmentVariable("PATH", binDir + Path.PathSeparator + path);
            try
            {
                var discovery = new BinaryDiscoveryService();
                var result = discovery.Discover(new BinaryDiscovery { Commands = ["no-version-cli"], VersionFlag = "--version" });

                Assert.Equal(AdapterDetectionState.Broken, result.State);
                Assert.NotNull(result.BinaryPath);
                Assert.True(string.IsNullOrEmpty(result.Version));
            }
            finally { Environment.SetEnvironmentVariable("PATH", path); }
        }
        finally { try { Directory.Delete(binDir, true); } catch { } }
    }

    [Fact]
    public void Discover_VersionFromStderr_IsTolerated()
    {
        if (OperatingSystem.IsWindows()) return;
        var binDir = NewDir();
        try
        {
            MakeExecutable(binDir, "stderr-cli", "echo '3.1.0' >&2; exit 1");
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            Environment.SetEnvironmentVariable("PATH", binDir + Path.PathSeparator + path);
            try
            {
                var discovery = new BinaryDiscoveryService();
                var result = discovery.Discover(new BinaryDiscovery { Commands = ["stderr-cli"], VersionFlag = "--version" });

                Assert.Equal(AdapterDetectionState.Detected, result.State);
                Assert.Equal("3.1.0", result.Version);
            }
            finally { Environment.SetEnvironmentVariable("PATH", path); }
        }
        finally { try { Directory.Delete(binDir, true); } catch { } }
    }

    [Fact]
    public void Discover_ExpandsTildeInWellKnownPaths()
    {
        if (OperatingSystem.IsWindows()) return;
        var homeSub = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cove-test-wk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(homeSub);
        try
        {
            MakeExecutable(homeSub, "tilde-cli", "echo '1.5.0'");
            var discovery = new BinaryDiscoveryService();
            var result = discovery.Discover(new BinaryDiscovery
            {
                Commands = ["tilde-cli"],
                VersionFlag = "--version",
            }, wellKnownPaths: ["~/" + Path.GetFileName(homeSub)]);

            Assert.Equal(AdapterDetectionState.Detected, result.State);
        }
        finally { try { Directory.Delete(homeSub, true); } catch { } }
    }

    [Fact]
    public void Discover_HangingVersionProbe_ReturnsWithinTimeout()
    {
        if (OperatingSystem.IsWindows()) return;
        var wkDir = NewDir();
        try
        {
            MakeExecutable(wkDir, "hang-cli", "echo '4.2.0'; exec sleep 20");
            var discovery = new BinaryDiscoveryService();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = discovery.Discover(new BinaryDiscovery
            {
                Commands = ["hang-cli"],
                VersionFlag = "--version",
            }, wellKnownPaths: [wkDir]);
            sw.Stop();

            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"probe took {sw.Elapsed}");
            Assert.Equal(AdapterDetectionState.Detected, result.State);
            Assert.Equal("4.2.0", result.Version);
        }
        finally { try { Directory.Delete(wkDir, true); } catch { } }
    }
}
