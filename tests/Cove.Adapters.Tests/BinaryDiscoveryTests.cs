using Cove.Adapters;
using Cove.Platform;
using Cove.Testing;
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

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public void Discover_FindsBinaryOnPath()
    {
        var binDir = NewDir();
        try
        {
            MakeExecutable(binDir, "fake-cli", "echo '1.0.0'");
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            var discovery = new BinaryDiscoveryService();
            var result = discovery.Discover(
                new BinaryDiscovery { Commands = ["fake-cli"], VersionFlag = "--version" },
                loginShellPath: binDir + Path.PathSeparator + path);

            Assert.Equal(AdapterDetectionState.Detected, result.State);
            Assert.NotNull(result.BinaryPath);
            Assert.Equal("1.0.0", result.Version);
        }
        finally { TestDirectory.Delete(binDir); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public void Discover_UsesLoginShellPathForShebangInterpreter()
    {
        var binDir = NewDir();
        try
        {
            Directory.CreateDirectory(binDir);
            var interpreter = Path.Combine(binDir, "test-node");
            File.WriteAllText(interpreter, "#!/bin/sh\nexec /bin/sh \"$@\"\n");
            var harness = Path.Combine(binDir, "script-harness");
            File.WriteAllText(harness, "#!/usr/bin/env test-node\necho '5.6.7'\n");
            if (!OperatingSystem.IsWindows())
            {
                System.IO.File.SetUnixFileMode(interpreter, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);
                System.IO.File.SetUnixFileMode(harness, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);
            }

            var discovery = new BinaryDiscoveryService();
            var result = discovery.Discover(
                new BinaryDiscovery { Commands = ["script-harness"], VersionFlag = "--version" },
                loginShellPath: binDir);

            Assert.Equal(AdapterDetectionState.Detected, result.State);
            Assert.Equal("5.6.7", result.Version);
        }
        finally { TestDirectory.Delete(binDir); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public void Discover_FindsBinaryInWellKnownPath()
    {
        var wkDir = NewDir();
        try
        {
            MakeExecutable(wkDir, "wk-cli", "echo '2.0.0'");
            var discovery = new BinaryDiscoveryService();
            var result = discovery.Discover(new BinaryDiscovery
            {
                Commands = ["wk-cli"],
                WellKnownPaths = [wkDir],
                VersionFlag = "--version",
            });

            Assert.Equal(AdapterDetectionState.Detected, result.State);
            Assert.Contains("wk-cli", result.BinaryPath ?? "");
        }
        finally { TestDirectory.Delete(wkDir); }
    }

    [Fact]
    public void Discover_MissingBinary_ReturnsMissingState()
    {
        var discovery = new BinaryDiscoveryService();
        var result = discovery.Discover(new BinaryDiscovery
        {
            Commands = ["definitely-not-a-real-binary-xyz"],
            WellKnownPaths = [],
            VersionFlag = "--version",
        });

        Assert.Equal(AdapterDetectionState.Missing, result.State);
        Assert.Null(result.BinaryPath);
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public void Discover_BinaryWithNoVersion_ReturnsBrokenState()
    {
        var binDir = NewDir();
        try
        {
            MakeExecutable(binDir, "no-version-cli", "exit 0");
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            var discovery = new BinaryDiscoveryService();
            var result = discovery.Discover(
                new BinaryDiscovery { Commands = ["no-version-cli"], VersionFlag = "--version" },
                loginShellPath: binDir + Path.PathSeparator + path);

            Assert.Equal(AdapterDetectionState.Broken, result.State);
            Assert.NotNull(result.BinaryPath);
            Assert.True(string.IsNullOrEmpty(result.Version));
        }
        finally { TestDirectory.Delete(binDir); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public void Discover_VersionFromStderr_IsTolerated()
    {
        var binDir = NewDir();
        try
        {
            MakeExecutable(binDir, "stderr-cli", "echo '3.1.0' >&2; exit 1");
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            var discovery = new BinaryDiscoveryService();
            var result = discovery.Discover(
                new BinaryDiscovery { Commands = ["stderr-cli"], VersionFlag = "--version" },
                loginShellPath: binDir + Path.PathSeparator + path);

            Assert.Equal(AdapterDetectionState.Detected, result.State);
            Assert.Equal("3.1.0", result.Version);
        }
        finally { TestDirectory.Delete(binDir); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public void Discover_ExpandsTildeInWellKnownPaths()
    {
        var home = NewDir();
        var homeSub = Path.Combine(home, ".cove-test-wk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(homeSub);
        try
        {
            MakeExecutable(homeSub, "tilde-cli", "echo '1.5.0'");
            var discovery = new BinaryDiscoveryService(environment: new TestRuntimeEnvironment(home));
            var result = discovery.Discover(new BinaryDiscovery
            {
                Commands = ["tilde-cli"],
                WellKnownPaths = ["~/" + Path.GetFileName(homeSub)],
                VersionFlag = "--version",
            });

            Assert.Equal(AdapterDetectionState.Detected, result.State);
        }
        finally { TestDirectory.Delete(home); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public void Discover_HangingVersionProbe_ReturnsWithinTimeout()
    {
        var wkDir = NewDir();
        try
        {
            MakeExecutable(wkDir, "hang-cli", "echo '4.2.0'; exec sleep 20");
            var discovery = new BinaryDiscoveryService();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = discovery.Discover(new BinaryDiscovery
            {
                Commands = ["hang-cli"],
                WellKnownPaths = [wkDir],
                VersionFlag = "--version",
            });
            sw.Stop();

            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"probe took {sw.Elapsed}");
            Assert.Equal(AdapterDetectionState.Detected, result.State);
            Assert.Equal("4.2.0", result.Version);
        }
        finally { TestDirectory.Delete(wkDir); }
    }
}

internal sealed class TestRuntimeEnvironment(string homeDirectory) : IRuntimeEnvironment
{
    public bool IsWindows => OperatingSystem.IsWindows();
    public string? ExecutablePath => Environment.GetEnvironmentVariable("PATH");
    public string HomeDirectory => homeDirectory;
    public string SystemDirectory => Environment.GetFolderPath(Environment.SpecialFolder.System);
    public IReadOnlyList<string> WindowsGitRoots => [];
}
