using System.Net;
using Cove.Adapters;
using Cove.Platform;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class AdapterRuntimeSeamTests
{
    [Fact]
    public void BinaryDiscovery_UsesInjectedFilesystemEnvironmentProcessAndManifestRegex()
    {
        var files = new FakePlatformFileSystem("/well-known/test-cli");
        var environment = new FakeRuntimeEnvironment
        {
            ExecutablePath = "/path-only",
            HomeDirectory = "/home/test",
        };
        var process = new FakeProcessRunner(new ProcessRunResult(true, false, 0, "test version release-7.4.2", "", 5));
        var service = new BinaryDiscoveryService(fileSystem: files, processRunner: process, environment: environment);

        var result = service.Discover(new BinaryDiscovery
        {
            Commands = ["test-cli"],
            WellKnownPaths = ["/well-known"],
            VersionFlag = "version",
            VersionRegex = @"release-(\d+\.\d+\.\d+)",
        });

        Assert.Equal(AdapterDetectionState.Detected, result.State);
        Assert.Equal("/well-known/test-cli", result.BinaryPath);
        Assert.Equal("7.4.2", result.Version);
        var request = Assert.Single(process.Requests);
        Assert.Equal("/well-known/test-cli", request.FileName);
        Assert.Equal(["version"], request.Arguments);
    }

    [Fact]
    public void BinaryDiscovery_ResolvesWindowsExecutableExtensionFromPath()
    {
        var directory = "tools";
        var executable = Path.Combine(directory, "test-cli.exe");
        var files = new FakePlatformFileSystem(executable);
        var environment = new FakeRuntimeEnvironment
        {
            IsWindows = true,
            ExecutablePath = directory,
        };
        var process = new FakeProcessRunner(new ProcessRunResult(true, false, 0, "test-cli 3.2.1", "", 5));
        var service = new BinaryDiscoveryService(fileSystem: files, processRunner: process, environment: environment);

        var result = service.Discover(new BinaryDiscovery
        {
            Commands = ["test-cli"],
            VersionFlag = "--version",
        });

        Assert.Equal(AdapterDetectionState.Detected, result.State);
        Assert.Equal(executable, result.BinaryPath);
        Assert.Equal(executable, Assert.Single(process.Requests).FileName);
    }

    [Fact]
    public void BinaryDiscovery_SkipsExtensionlessWindowsShimAndProbesCmdThroughCommandProcessor()
    {
        var directory = "tools";
        var extensionless = Path.Combine(directory, "test-cli");
        var commandShim = Path.Combine(directory, "test-cli.cmd");
        var files = new FakePlatformFileSystem(extensionless, commandShim);
        var environment = new FakeRuntimeEnvironment
        {
            IsWindows = true,
            ExecutablePath = directory,
        };
        var process = new FakeProcessRunner(new ProcessRunResult(true, false, 0, "test-cli 3.2.1", "", 5));
        var service = new BinaryDiscoveryService(fileSystem: files, processRunner: process, environment: environment);

        var result = service.Discover(new BinaryDiscovery
        {
            Commands = ["test-cli"],
            VersionFlag = "--version",
        });

        Assert.Equal(AdapterDetectionState.Detected, result.State);
        Assert.Equal(commandShim, result.BinaryPath);
        var request = Assert.Single(process.Requests);
        Assert.Equal("cmd.exe", request.FileName);
        Assert.Equal(["/d", "/s", "/c", commandShim, "--version"], request.Arguments);
        Assert.DoesNotContain(extensionless, files.Probes);
    }

    [Fact]
    public void BinaryDiscovery_SkipsBrokenWindowsShimForRunnableLaterCandidate()
    {
        var staleDirectory = "stale";
        var workingDirectory = "working";
        var staleShim = Path.Combine(staleDirectory, "test-cli.cmd");
        var workingExecutable = Path.Combine(workingDirectory, "test-cli.exe");
        var files = new FakePlatformFileSystem(staleShim, workingExecutable);
        var environment = new FakeRuntimeEnvironment
        {
            IsWindows = true,
            ExecutablePath = staleDirectory + ";" + workingDirectory,
        };
        var process = new FakeProcessRunner(
            new ProcessRunResult(true, false, 1, "", "missing runtime", 5),
            new ProcessRunResult(true, false, 0, "test-cli 4.5.6", "", 5));
        var service = new BinaryDiscoveryService(fileSystem: files, processRunner: process, environment: environment);

        var result = service.Discover(new BinaryDiscovery
        {
            Commands = ["test-cli"],
            VersionFlag = "--version",
        });

        Assert.Equal(AdapterDetectionState.Detected, result.State);
        Assert.Equal(workingExecutable, result.BinaryPath);
        Assert.Equal("4.5.6", result.Version);
        Assert.Equal("cmd.exe", process.Requests[0].FileName);
        Assert.Equal(workingExecutable, process.Requests[1].FileName);
    }

    [Fact]
    public void BinaryDiscovery_UsesWindowsPathSourcesFallbacksAndPathExtOrder()
    {
        var appDataNpm = Path.Combine("/appdata", "npm");
        var commandShim = Path.Combine(appDataNpm, "test-cli.cmd");
        var files = new FakePlatformFileSystem(commandShim);
        var environment = new FakeRuntimeEnvironment
        {
            IsWindows = true,
            ExecutablePath = "/process",
            UserExecutablePath = "/user",
            MachineExecutablePath = "/machine",
            PathExtensions = "CMD;EXE",
            Variables = new Dictionary<string, string?>
            {
                ["APPDATA"] = "/appdata",
            },
        };
        var process = new FakeProcessRunner(new ProcessRunResult(true, false, 0, "test-cli 8.1.0", "", 5));
        var service = new BinaryDiscoveryService(fileSystem: files, processRunner: process, environment: environment);

        var result = service.Discover(
            new BinaryDiscovery
            {
                Commands = ["test-cli"],
                VersionFlag = "--version",
            },
            loginShellPath: "/login");

        Assert.Equal(AdapterDetectionState.Detected, result.State);
        Assert.Equal(commandShim, result.BinaryPath);
        Assert.Equal(
            [
                Path.Combine("/login", "test-cli.cmd"),
                Path.Combine("/login", "test-cli.exe"),
                Path.Combine("/process", "test-cli.cmd"),
                Path.Combine("/process", "test-cli.exe"),
                Path.Combine("/user", "test-cli.cmd"),
                Path.Combine("/user", "test-cli.exe"),
                Path.Combine("/machine", "test-cli.cmd"),
                Path.Combine("/machine", "test-cli.exe"),
            ],
            files.Probes.Take(8));
        Assert.Equal("cmd.exe", Assert.Single(process.Requests).FileName);
    }

    [Fact]
    public void BinaryDiscovery_UsesMacOsFallbackBeforeManifestSpecificPath()
    {
        var fallbackExecutable = Path.Combine("/opt/homebrew/bin", "test-cli");
        var manifestExecutable = Path.Combine("/adapter", "test-cli");
        var files = new FakePlatformFileSystem(fallbackExecutable, manifestExecutable);
        var environment = new FakeRuntimeEnvironment
        {
            IsMacOS = true,
            ExecutablePath = "/process",
        };
        var process = new FakeProcessRunner(new ProcessRunResult(true, false, 0, "test-cli 8.2.0", "", 5));
        var service = new BinaryDiscoveryService(fileSystem: files, processRunner: process, environment: environment);

        var result = service.Discover(new BinaryDiscovery
        {
            Commands = ["test-cli"],
            WellKnownPaths = ["/adapter"],
            VersionFlag = "--version",
        });

        Assert.Equal(AdapterDetectionState.Detected, result.State);
        Assert.Equal(fallbackExecutable, result.BinaryPath);
        Assert.DoesNotContain(manifestExecutable, files.Probes);
    }

    [Fact]
    public void BashLocator_UsesInjectedRuntimeView()
    {
        var files = new FakePlatformFileSystem("/custom/bin/bash.exe");
        var environment = new FakeRuntimeEnvironment
        {
            IsWindows = true,
            ExecutablePath = "/system32:/custom/bin",
            SystemDirectory = "/system32",
            WindowsGitRoots = ["/program-files"],
        };

        var bash = new BashLocator(files, environment).Find();

        Assert.Equal("/custom/bin/bash.exe", bash);
        Assert.Contains("/program-files/Git/bin/bash.exe", files.Probes);
        Assert.DoesNotContain("/system32/bash.exe", files.Probes);
    }

    [Fact]
    public async Task MethodRunner_UsesInjectedBashAndProcessWithoutShellConcatenation()
    {
        var process = new FakeProcessRunner(new ProcessRunResult(true, false, 0, "{\"ok\":true}", "", 12));
        var runner = new MethodRunner(new FakeBashLocator("/bin/custom-bash"), process);

        var result = await runner.RunAsync(
            "/adapter root",
            "script.sh",
            ["argument with spaces", "$(not-executed)"],
            TimeSpan.FromSeconds(2),
            new Dictionary<string, string> { ["CUSTOM"] = "value" });

        Assert.True(result.Ok);
        Assert.True(result.Json.HasValue);
        var request = Assert.Single(process.Requests);
        Assert.Equal("/bin/custom-bash", request.FileName);
        Assert.Equal(["/adapter root/script.sh", "argument with spaces", "$(not-executed)"], request.Arguments);
        Assert.Equal("/adapter root", request.Environment!["COVE_ADAPTER_DIR"]);
        Assert.Equal("2", request.Environment["COVE_SDK_VERSION"]);
        Assert.Equal("value", request.Environment["CUSTOM"]);
    }

    [Fact]
    public async Task AdapterInstall_UsesInjectedExecutableModeBashAndProcess()
    {
        var source = Path.Combine(Path.GetTempPath(), "cove-seam-source-" + Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(Path.GetTempPath(), "cove-seam-destination-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);
        try
        {
            File.WriteAllText(Path.Combine(source, "adapter.json"), """
            { "sdkVersion": 2, "name": "test-adapter", "displayName": "Test", "description": "", "accent": "#123456", "binary": "test", "version": "1.0.0", "methods": {} }
            """);
            File.WriteAllText(Path.Combine(source, "hooks.sh"), "exit 0");
            File.WriteAllText(Path.Combine(source, "nested.sh"), "exit 0");
            var modes = new RecordingExecutableMode();
            var process = new FakeProcessRunner(new ProcessRunResult(true, false, 0, "", "", 1));
            var service = new AdapterInstallService(
                SystemPlatformFileSystem.Instance,
                modes,
                new FakeBashLocator("/bin/custom-bash"),
                process);

            await service.InstallAsync(destination, "test-adapter", new LocalDirAdapterFetcher(source));

            Assert.Equal(2, modes.Paths.Count);
            Assert.All(modes.Paths, path => Assert.EndsWith(".sh", path, StringComparison.Ordinal));
            var request = Assert.Single(process.Requests);
            Assert.Equal("/bin/custom-bash", request.FileName);
            Assert.Equal("install", request.Arguments[1]);
            Assert.Equal(Path.Combine(destination, "test-adapter"), request.WorkingDirectory);
        }
        finally
        {
            if (Directory.Exists(source))
                Directory.Delete(source, true);
            if (Directory.Exists(destination))
                Directory.Delete(destination, true);
        }
    }

    [Fact]
    public async Task NpmRegistryClient_UsesInjectedHttpClientAndEndpoint()
    {
        var handler = new RecordingHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"version\":\"3.2.1\"}"),
        });
        using var http = new HttpClient(handler);
        var client = new NpmHarnessRegistryClient(http, new Uri("https://packages.example.test/npm/"));

        var version = await client.GetLatestVersionAsync("@scope/tool");

        Assert.Equal("3.2.1", version);
        Assert.Equal("https://packages.example.test/npm/%40scope%2Ftool/latest", handler.Request!.RequestUri!.AbsoluteUri);
    }

    private sealed class FakePlatformFileSystem(params string[] files) : IPlatformFileSystem
    {
        private readonly HashSet<string> _files = files.ToHashSet(StringComparer.Ordinal);
        public List<string> Probes { get; } = [];
        public bool FileExists(string path) { Probes.Add(path); return _files.Contains(path); }
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

    private sealed class FakeRuntimeEnvironment : IRuntimeEnvironment
    {
        public bool IsWindows { get; init; }
        public bool IsMacOS { get; init; }
        public bool IsLinux { get; init; }
        public string? ExecutablePath { get; init; }
        public string? UserExecutablePath { get; init; }
        public string? MachineExecutablePath { get; init; }
        public string? PathExtensions { get; init; }
        public string HomeDirectory { get; init; } = "/home";
        public string SystemDirectory { get; init; } = "/system32";
        public IReadOnlyList<string> WindowsGitRoots { get; init; } = [];
        public IReadOnlyDictionary<string, string?> Variables { get; init; } = new Dictionary<string, string?>();
        public string? GetEnvironmentVariable(string name) => Variables.GetValueOrDefault(name);
    }

    private sealed class FakeProcessRunner(params ProcessRunResult[] results) : IProcessRunner
    {
        private int _nextResult;
        public List<ProcessRunRequest> Requests { get; } = [];
        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var index = Math.Min(_nextResult++, results.Length - 1);
            return Task.FromResult(results[index]);
        }
    }

    private sealed class FakeBashLocator(string? path) : IBashLocator
    {
        public string? Find() => path;
    }

    private sealed class RecordingExecutableMode : IExecutableMode
    {
        public List<string> Paths { get; } = [];
        public void MakeUserExecutable(string path) => Paths.Add(path);
    }

    private sealed class RecordingHttpHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(response);
        }
    }
}
