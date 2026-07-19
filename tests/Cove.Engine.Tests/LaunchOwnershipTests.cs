using System.Text.Json;
using Cove.Adapters;
using Cove.Engine.Launch;
using Cove.Engine.Restart;
using Cove.Testing;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LaunchOwnershipTests
{
    [Fact]
    public void CommandComposer_OwnsProfileOverridesAndWorkingDirectory()
    {
        var composer = new LaunchCommandComposer();
        var profile = NewProfile() with { CliArgs = ["claude", "--profile-flag"] };
        var overrides = new LauncherOverrides
        {
            Yolo = true,
            WorkingDir = "/worktree",
            ExtraFlags = ["--extra"],
            Env = new Dictionary<string, string> { ["COVE_TEST"] = "1" },
        };

        var command = composer.Compose(profile, overrides);

        Assert.Equal("claude", command.Command);
        Assert.Equal(
            ["--profile-flag", "--dangerously-skip-permissions", "--extra", "--env=COVE_TEST=1"],
            command.Args);
        Assert.Equal("/worktree", command.Cwd);
    }

    [Fact]
    public void ProfileLookup_PrefersStoredProfileBeforeSynthesizedDefault()
    {
        var root = NewDirectory();
        try
        {
            var store = new LaunchProfileStore(root);
            store.Save(NewProfile() with { Name = "Stored", Slug = "default", IsDefault = true });
            var lookup = new LaunchProfileLookup(store);

            var stored = lookup.Find("claude-code", "default");
            var synthesized = lookup.Find("codex", "default");

            Assert.Equal("Stored", stored!.Name);
            Assert.Equal("Default", synthesized!.Name);
            Assert.Equal("codex", synthesized.Adapter);
            Assert.Null(lookup.Find("codex", "missing"));
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void AdapterLookup_OwnsManifestAndDirectoryResolution()
    {
        var root = NewDirectory();
        try
        {
            WriteManifest(root, "claude-code");
            var lookup = new LaunchAdapterLookup(new AdapterManifestStore(root));

            var adapter = lookup.Find("claude-code");

            Assert.NotNull(adapter);
            Assert.Equal("claude-code", adapter!.Manifest.Name);
            Assert.Equal(Path.Combine(root, "claude-code"), adapter.Directory);
            Assert.Null(lookup.Find("missing"));
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void LauncherOptionsParser_OwnsWireShapeParsing()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "options": [
                {
                  "key": "model",
                  "label": "Model",
                  "type": "select",
                  "default": "sonnet",
                  "choices": [
                    {"value": "sonnet", "label": "Sonnet"},
                    "opus"
                  ]
                }
              ],
              "suggestedFlags": [
                {"flag": "--verbose", "description": "Verbose", "values": ["true"]}
              ]
            }
            """);

        var options = new LauncherOptionsParser().Parse(document.RootElement);

        Assert.NotNull(options);
        var option = Assert.Single(options!.Options);
        Assert.Equal("sonnet", option.DefaultValueRaw);
        Assert.Equal(
            [new LauncherOptionChoice("sonnet", "Sonnet"), new LauncherOptionChoice("opus", null)],
            option.Choices);
        var suggested = Assert.Single(options.SuggestedFlags);
        Assert.Equal("--verbose", suggested.Flag);
        Assert.Equal(["true"], suggested.Values);
    }

    [Fact]
    public async Task LauncherOptionsResolver_OwnsAdapterOptionSource()
    {
        var root = NewDirectory();
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(
                Path.Combine(root, "options.json"),
                """{"options":[{"key":"verbose","label":"Verbose","type":"toggle","default":false}]}""");
            var adapter = new LaunchAdapter(
                new AdapterManifest
                {
                    SdkVersion = 2,
                    Name = "claude-code",
                    DisplayName = "Claude",
                    Description = "test",
                    Accent = "#000000",
                    Binary = "claude",
                    Version = "1",
                    Methods = new Dictionary<string, AdapterMethod>
                    {
                        ["launcher_options"] = new() { Static = "options.json" },
                    },
                },
                root);
            var resolver = new LauncherOptionsResolver(
                new StubLaunchAdapterLookup(adapter),
                new StubLaunchProcessAcquirer("unused"),
                new LauncherOptionsParser());

            var options = await resolver.LoadAsync("claude-code");

            Assert.Equal("false", Assert.Single(options!.Options).DefaultValueRaw);
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(root);
        }
    }

    [Fact]
    public async Task Orchestrator_CoordinatesInjectedLaunchOwners()
    {
        var adapter = new LaunchAdapter(
            new AdapterManifest
            {
                SdkVersion = 2,
                Name = "claude-code",
                DisplayName = "Claude",
                Description = "test",
                Accent = "#000000",
                Binary = "claude",
                Version = "1",
                Methods = new Dictionary<string, AdapterMethod>(),
                BinaryDiscovery = new BinaryDiscovery { Commands = ["claude"] },
            },
            "/adapter");
        var processes = new StubLaunchProcessAcquirer("/resolved/claude");
        var orchestrator = new LaunchOrchestrator(
            new LaunchCommandComposer(),
            new StubLaunchAdapterLookup(adapter),
            processes);

        var command = await orchestrator.BuildLaunchCommandAsync(
            NewProfile(),
            new LauncherOverrides { ExtraFlags = ["--verbose"], WorkingDir = "/cwd" });

        Assert.True(processes.AcquireCalled);
        Assert.Equal("/resolved/claude", command.Command);
        Assert.Equal(["--verbose"], command.Args);
        Assert.Equal("/cwd", command.Cwd);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task ProcessAcquirer_OwnsDetectBinaryMethodExecution()
    {
        var root = NewDirectory();
        try
        {
            Directory.CreateDirectory(root);
            var script = Path.Combine(root, "detect.sh");
            var binary = Path.Combine(root, "resolved-cli");
            File.WriteAllText(
                script,
                $"#!/usr/bin/env bash\nprintf '{{\"path\":\"{binary}\"}}'\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    script,
                    UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute);
            }
            var adapter = new LaunchAdapter(
                new AdapterManifest
                {
                    SdkVersion = 1,
                    Name = "legacy",
                    DisplayName = "Legacy",
                    Description = "test",
                    Accent = "#000000",
                    Binary = "legacy",
                    Version = "1",
                    Methods = new Dictionary<string, AdapterMethod>
                    {
                        ["detect_binary"] = new() { Script = "detect.sh" },
                    },
                },
                root);
            var processes = new LaunchProcessAcquirer(
                new MethodRunner(),
                new BinaryDiscoveryService());

            var resolved = await processes.AcquireBinaryAsync(adapter);

            Assert.Equal(binary, resolved);
        }
        finally
        {
            Cove.Testing.TestDirectory.Delete(root);
        }
    }

    private static LaunchProfile NewProfile() => new(
        "Test",
        "test",
        "claude-code",
        false,
        null,
        null,
        [],
        new Dictionary<string, string>(),
        new Dictionary<string, bool>(),
        [],
        null,
        1);

    private static string NewDirectory()
        => Path.Combine(Path.GetTempPath(), "cove-launch-ownership-" + Guid.NewGuid().ToString("N"));

    private static void WriteManifest(string root, string adapter)
    {
        var directory = Path.Combine(root, adapter);
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "adapter.json"),
            $$"""
              {
                "sdkVersion": 2,
                "name": "{{adapter}}",
                "displayName": "Test",
                "description": "test",
                "accent": "#000000",
                "binary": "test",
                "version": "1.0.0",
                "methods": {},
                "binaryDiscovery": {
                  "commands": ["test"],
                  "wellKnownPaths": [],
                  "versionFlag": "--version"
                }
              }
              """);
    }

    private sealed class StubLaunchAdapterLookup(LaunchAdapter adapter) : ILaunchAdapterLookup
    {
        public LaunchAdapter? Find(string adapterName) => adapter;
    }

    private sealed class StubLaunchProcessAcquirer(string binary) : ILaunchProcessAcquirer
    {
        public bool AcquireCalled { get; private set; }

        public Task<MethodResult> RunMethodAsync(
            LaunchAdapter adapter,
            string methodName,
            string script,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No method expected.");

        public Task<string?> AcquireBinaryAsync(
            LaunchAdapter adapter,
            CancellationToken cancellationToken = default)
        {
            AcquireCalled = true;
            return Task.FromResult<string?>(binary);
        }

        public BinaryDiscoveryResult Describe(AdapterManifest manifest)
            => new(AdapterDetectionState.Detected, binary, null);

        public void RefreshLoginShellPath()
        {
        }
    }
}
