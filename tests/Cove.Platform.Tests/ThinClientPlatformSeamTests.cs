using System.Diagnostics;
using System.Runtime.InteropServices;
using Cove.Platform;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class ThinClientPlatformSeamTests
{
    [Fact]
    public void SystemEngineProcessLauncher_IsCanonicalSingletonImplementation()
    {
        Assert.IsAssignableFrom<IEngineProcessLauncher>(SystemEngineProcessLauncher.Instance);
        Assert.Same(SystemEngineProcessLauncher.Instance, SystemEngineProcessLauncher.Instance);
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, ".exe")]
    public void SystemEngineProcessLauncher_UsesOverrideBeforeBundledCandidates(bool isWindows, string suffix)
    {
        var overridePath = Path.Combine("override", "engine" + suffix);
        var files = new FakePlatformFileSystem(
            overridePath,
            Path.Combine("app", "cove-engine" + suffix),
            Path.Combine("app", "cove" + suffix));
        ProcessStartInfo? request = null;
        var launcher = new SystemEngineProcessLauncher(
            files,
            name => name == "COVE_ENGINE" ? overridePath : null,
            startInfo => request = startInfo,
            isWindows,
            "app");

        launcher.Launch("dev channel");

        Assert.Equal([overridePath], files.Probes);
        Assert.NotNull(request);
        Assert.Equal(overridePath, request.FileName);
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, ".exe")]
    public void SystemEngineProcessLauncher_ProbesRenamedThenBundledExecutable(bool isWindows, string suffix)
    {
        var renamed = Path.Combine("app", "cove-engine" + suffix);
        var bundled = Path.Combine("app", "cove" + suffix);
        var files = new FakePlatformFileSystem(bundled);
        ProcessStartInfo? request = null;
        var launcher = new SystemEngineProcessLauncher(
            files,
            _ => Path.Combine("missing", "override"),
            startInfo => request = startInfo,
            isWindows,
            "app");

        launcher.Launch("stable");

        Assert.Equal([Path.Combine("missing", "override"), renamed, bundled], files.Probes);
        Assert.NotNull(request);
        Assert.Equal(bundled, request.FileName);
    }

    [Fact]
    public void SystemEngineProcessLauncher_CreatesDetachedDaemonRunRequest()
    {
        var executable = Path.Combine("app", "cove-engine");
        var files = new FakePlatformFileSystem(executable);
        ProcessStartInfo? request = null;
        var launcher = new SystemEngineProcessLauncher(
            files,
            _ => "",
            startInfo => request = startInfo,
            false,
            "app");

        launcher.Launch("channel with spaces");

        Assert.NotNull(request);
        Assert.Equal(executable, request.FileName);
        Assert.Equal(["daemon", "run", "--channel", "channel with spaces"], request.ArgumentList);
        Assert.False(request.UseShellExecute);
        Assert.True(request.CreateNoWindow);
        Assert.False(request.RedirectStandardInput);
        Assert.False(request.RedirectStandardOutput);
        Assert.False(request.RedirectStandardError);
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, ".exe")]
    public void SystemEngineProcessLauncher_ThrowsWhenNoExecutableExists(bool isWindows, string suffix)
    {
        var files = new FakePlatformFileSystem();
        var starts = 0;
        var launcher = new SystemEngineProcessLauncher(
            files,
            _ => null,
            _ => starts++,
            isWindows,
            "app");

        var error = Assert.Throws<FileNotFoundException>((Action)(() => launcher.Launch("dev")));

        Assert.Contains("COVE_ENGINE", error.Message, StringComparison.Ordinal);
        Assert.Equal(
            [Path.Combine("app", "cove-engine" + suffix), Path.Combine("app", "cove" + suffix)],
            files.Probes);
        Assert.Equal(0, starts);
    }

    [Fact]
    public void SystemShellResolver_ReturnsPowerShellOnWindowsWithoutReadingShell()
    {
        var environmentReads = 0;
        var resolver = new SystemShellResolver(true, _ =>
        {
            environmentReads++;
            return "/bin/fish";
        });

        var shell = resolver.ResolveDefaultShell();

        Assert.IsAssignableFrom<IShellResolver>(resolver);
        Assert.Equal("powershell.exe", shell);
        Assert.Equal(0, environmentReads);
    }

    [Theory]
    [InlineData("/bin/fish", "/bin/fish")]
    [InlineData(null, "/bin/zsh")]
    [InlineData("", "/bin/zsh")]
    public void SystemShellResolver_ReturnsShellEnvironmentOrZshOnUnix(string? configured, string expected)
    {
        string? requestedVariable = null;
        var resolver = new SystemShellResolver(false, name =>
        {
            requestedVariable = name;
            return configured;
        });

        var shell = resolver.ResolveDefaultShell();

        Assert.Equal("SHELL", requestedVariable);
        Assert.Equal(expected, shell);
    }

    [Theory]
    [InlineData("/tools/PowerShell.exe")]
    [InlineData("/tools/PWSH")]
    public void ShellInvocation_CreateUsesExactPowerShellFlags(string shell)
    {
        var invocation = ShellInvocation.Create(shell, "Write-Output 'hello world'");

        Assert.Equal(shell, invocation.Command);
        Assert.Equal(["-NoLogo", "-Command", "Write-Output 'hello world'"], invocation.Args);
    }

    [Fact]
    public void ShellInvocation_CreateUsesExactCmdFlags()
    {
        const string shell = "/tools/CMD.exe";

        var invocation = ShellInvocation.Create(shell, "echo hello & echo world");

        Assert.Equal(shell, invocation.Command);
        Assert.Equal(["/c", "echo hello & echo world"], invocation.Args);
    }

    [Theory]
    [InlineData("/bin/zsh")]
    [InlineData("/bin/bash")]
    [InlineData("/opt/homebrew/bin/fish")]
    public void ShellInvocation_CreateUsesExactLoginShellFlags(string shell)
    {
        var invocation = ShellInvocation.Create(shell, "printf '%s' \"hello world\"");

        Assert.Equal(shell, invocation.Command);
        Assert.Equal(["-ilc", "printf '%s' \"hello world\""], invocation.Args);
    }

    [Fact]
    public void ShutdownSignalRegistration_ReturnsNoOpOwnerOnWindows()
    {
        var registrations = 0;
        var shutdownRequests = 0;
        var owner = ShutdownSignalRegistration.Register(
            () => shutdownRequests++,
            true,
            (_, _) =>
            {
                registrations++;
                return new RecordingDisposable();
            });

        owner.Dispose();
        owner.Dispose();

        Assert.Equal(0, registrations);
        Assert.Equal(0, shutdownRequests);
    }

    [Fact]
    public void ShutdownSignalRegistration_OwnsOnlyInterruptAndTerminateRegistrationsOnUnix()
    {
        var shutdownRequests = 0;
        var registrations = new List<SignalRegistration>();
        var owner = ShutdownSignalRegistration.Register(
            () => shutdownRequests++,
            false,
            (signal, requestShutdown) =>
            {
                var registration = new SignalRegistration(signal, requestShutdown, new RecordingDisposable());
                registrations.Add(registration);
                return registration.Owner;
            });

        Assert.Equal([PosixSignal.SIGINT, PosixSignal.SIGTERM], registrations.Select(x => x.Signal));
        registrations[0].RequestShutdown();
        registrations[1].RequestShutdown();
        Assert.Equal(2, shutdownRequests);

        owner.Dispose();
        owner.Dispose();

        Assert.All(registrations, registration => Assert.Equal(1, registration.Owner.DisposeCount));
    }

    private sealed class FakePlatformFileSystem(params string[] files) : IPlatformFileSystem
    {
        private readonly HashSet<string> _files = files.ToHashSet(StringComparer.Ordinal);
        public List<string> Probes { get; } = [];

        public bool FileExists(string path)
        {
            Probes.Add(path);
            return _files.Contains(path);
        }

        public bool DirectoryExists(string path) => false;
        public void CreateDirectory(string path) => throw new NotSupportedException();
        public void DeleteFile(string path) => throw new NotSupportedException();
        public void DeleteDirectory(string path, bool recursive) => throw new NotSupportedException();
        public void MoveDirectory(string source, string destination) => throw new NotSupportedException();
        public IEnumerable<string> EnumerateFiles(string path, string pattern, SearchOption option) => [];
        public IEnumerable<string> EnumerateFileSystemEntries(string path) => [];
        public DateTimeOffset GetLastWriteTimeUtc(string path) => DateTimeOffset.UnixEpoch;
        public string ReadAllText(string path) => throw new NotSupportedException();
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void CopyFile(string source, string destination, bool overwrite) => throw new NotSupportedException();
    }

    private sealed class RecordingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed record SignalRegistration(
        PosixSignal Signal,
        Action RequestShutdown,
        RecordingDisposable Owner);
}
