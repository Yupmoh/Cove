using Cove.Adapters;
using Cove.Engine.Adapters;
using Cove.Platform;
using Cove.Testing;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AdapterUpdateCommandTests
{
    [Fact]
    public void BrewCellarUsesPathPackageSegment()
    {
        var result = Resolver().Resolve(
            Manifest("codex", brew: "declared-codex"),
            AdapterDetectionState.Detected,
            "/opt/homebrew/bin/codex",
            "/opt/homebrew/Cellar/codex/0.144.4/bin/codex");

        Assert.Equal("brew", result.Provenance);
        Assert.Equal("brew upgrade codex", result.UpdateCommand);
        Assert.Equal("brew uninstall codex", result.UninstallCommand);
    }

    [Fact]
    public void BrewCaskNormalizesDirectorySeparators()
    {
        var result = Resolver().Resolve(
            Manifest("claude-code", brew: "claude-code"),
            AdapterDetectionState.Detected,
            "C:\\homebrew\\bin\\claude",
            "C:\\homebrew\\Caskroom\\claude-code\\2.1.198\\claude.exe");

        Assert.Equal("brew", result.Provenance);
        Assert.Equal("brew upgrade claude-code", result.UpdateCommand);
    }

    [Fact]
    public void BrewDeclaredIdentityCoversUnavailablePackageSegment()
    {
        var result = Resolver().Resolve(
            Manifest("gemini", brew: "gemini-cli"),
            AdapterDetectionState.Detected,
            "/opt/homebrew/Cellar/gemini",
            "/opt/homebrew/Cellar/");

        Assert.Equal("brew", result.Provenance);
        Assert.Equal("brew upgrade gemini-cli", result.UpdateCommand);
    }

    [Fact]
    public void BunGlobalTargetRequiresDeclaredNpmIdentity()
    {
        var environment = new LifecycleRuntimeEnvironment
        {
            HomeDirectory = "/Users/test",
            IsMacOS = true,
        };
        var result = Resolver(environment).Resolve(
            Manifest("omp", npm: "@oh-my-pi/pi-coding-agent"),
            AdapterDetectionState.Detected,
            "/Users/test/.bun/bin/omp",
            "/Users/test/.bun/install/global/node_modules/@oh-my-pi/pi-coding-agent/dist/cli.js");

        Assert.Equal("bun", result.Provenance);
        Assert.Equal("bun install -g @oh-my-pi/pi-coding-agent@latest", result.UpdateCommand);
        Assert.Equal("bun remove -g @oh-my-pi/pi-coding-agent", result.UninstallCommand);
    }

    [Fact]
    public void UnixGlobalNodeModulesRequiresDeclaredNpmIdentity()
    {
        var result = Resolver().Resolve(
            Manifest("claude-code", npm: "@anthropic-ai/claude-code"),
            AdapterDetectionState.Detected,
            "/opt/homebrew/bin/claude",
            "/opt/homebrew/lib/node_modules/@anthropic-ai/claude-code/cli.js");

        Assert.Equal("npm", result.Provenance);
        Assert.Equal(
            "npm install -g --allow-scripts=@anthropic-ai/claude-code @anthropic-ai/claude-code@latest",
            result.UpdateCommand);
        Assert.Equal("npm uninstall -g @anthropic-ai/claude-code", result.UninstallCommand);
    }

    [Fact]
    public void WindowsNpmShimRequiresExactAdjacentPackageMetadata()
    {
        var root = NewDirectory();
        try
        {
            var npmRoot = Path.Combine(root, "npm");
            var shim = Path.Combine(npmRoot, "codex.cmd");
            var packageDirectory = Path.Combine(npmRoot, "node_modules", "@openai", "codex");
            Directory.CreateDirectory(packageDirectory);
            File.WriteAllText(shim, "@echo off");
            File.WriteAllText(Path.Combine(packageDirectory, "package.json"), "{\"name\":\"@openai/codex\"}");
            var environment = new LifecycleRuntimeEnvironment
            {
                IsWindows = true,
                Variables = new Dictionary<string, string?> { ["APPDATA"] = root },
            };

            var result = Resolver(environment).Resolve(
                Manifest("codex", npm: "@openai/codex"),
                AdapterDetectionState.Detected,
                shim,
                shim);

            Assert.Equal("npm", result.Provenance);
            Assert.Equal("npm uninstall -g @openai/codex", result.UninstallCommand);
            Assert.NotNull(result.UpdateCommand);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CopiedWindowsCommandShimHasUnknownProvenanceAndNoDestructiveCommands()
    {
        var root = NewDirectory();
        try
        {
            var shim = Path.Combine(root, "copied", "codex.cmd");
            Directory.CreateDirectory(Path.GetDirectoryName(shim)!);
            File.WriteAllText(shim, "@echo off");
            var environment = new LifecycleRuntimeEnvironment
            {
                IsWindows = true,
                Variables = new Dictionary<string, string?> { ["APPDATA"] = Path.Combine(root, "appdata") },
            };

            var result = Resolver(environment).Resolve(
                Manifest("codex", npm: "@openai/codex"),
                AdapterDetectionState.Detected,
                shim,
                shim);

            Assert.Equal("unknown", result.Provenance);
            Assert.Null(result.UpdateCommand);
            Assert.Null(result.UninstallCommand);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void WindowsNpmRejectsMismatchedAdjacentPackageMetadata()
    {
        var root = NewDirectory();
        try
        {
            var npmRoot = Path.Combine(root, "npm");
            var shim = Path.Combine(npmRoot, "codex.cmd");
            var packageDirectory = Path.Combine(npmRoot, "node_modules", "@openai", "codex");
            Directory.CreateDirectory(packageDirectory);
            File.WriteAllText(shim, "@echo off");
            File.WriteAllText(Path.Combine(packageDirectory, "package.json"), "{\"name\":\"other-package\"}");
            var environment = new LifecycleRuntimeEnvironment
            {
                IsWindows = true,
                Variables = new Dictionary<string, string?> { ["APPDATA"] = root },
            };

            var result = Resolver(environment).Resolve(
                Manifest("codex", npm: "@openai/codex"),
                AdapterDetectionState.Detected,
                shim,
                shim);

            Assert.Equal("unknown", result.Provenance);
            Assert.Null(result.UpdateCommand);
            Assert.Null(result.UninstallCommand);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void VerifiedNonShimBinaryWithoutPackageManagerEvidenceIsNative()
    {
        var result = Resolver().Resolve(
            Manifest("claude-code", npm: "@anthropic-ai/claude-code"),
            AdapterDetectionState.Detected,
            "/Users/test/.claude/local/claude",
            "/Users/test/.claude/local/claude");

        Assert.Equal("native", result.Provenance);
        Assert.Null(result.UpdateCommand);
        Assert.Null(result.UninstallCommand);
    }

    [Fact]
    public void MissingAdapterUsesDeclaredNpmIdentityForInstallOnly()
    {
        var result = Resolver().Resolve(
            Manifest("codex", npm: "@openai/codex"),
            AdapterDetectionState.Missing,
            null,
            null);

        Assert.Equal("unknown", result.Provenance);
        Assert.Equal(
            "npm install -g --allow-scripts=@openai/codex @openai/codex@latest",
            result.InstallCommand);
        Assert.Null(result.UpdateCommand);
        Assert.Null(result.UninstallCommand);
    }

    [PlatformTheory(TestOperatingSystem.MacOS)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    [InlineData("claude-code", "npm")]
    [InlineData("codex", "npm")]
    [InlineData("omp", "bun")]
    [InlineData("pi", "npm")]
    public void InstalledMacOsAdaptersResolveRealLifecycleProvenance(
        string adapterName,
        string expectedProvenance)
    {
        var adaptersRoot = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "adapters");
        var manifest = new AdapterManifestStore(adaptersRoot).Load(adapterName);
        Assert.NotNull(manifest);
        var detection = new BinaryDiscoveryService().Discover(manifest!.BinaryDiscovery!);
        Assert.Equal(AdapterDetectionState.Detected, detection.State);
        var realPath = AdapterListCommands.ResolveRealPath(detection.BinaryPath);

        var result = Resolver().Resolve(
            manifest,
            detection.State,
            detection.BinaryPath,
            realPath);

        Assert.Equal(expectedProvenance, result.Provenance);
        Assert.NotNull(result.UpdateCommand);
        Assert.NotNull(result.UninstallCommand);
    }

    [Fact]
    public void ExplicitRecipesOverrideGeneratedCommandsForEachOperation()
    {
        var manifest = Manifest("opencode", npm: "opencode-ai") with
        {
            Install = Recipes("custom install"),
            Update = Recipes("custom update"),
            Uninstall = Recipes("custom uninstall"),
        };

        var result = Resolver().Resolve(
            manifest,
            AdapterDetectionState.Detected,
            "/usr/local/bin/opencode",
            "/usr/local/lib/node_modules/opencode-ai/bin/opencode");

        Assert.Equal("npm", result.Provenance);
        Assert.Equal("custom install", result.InstallCommand);
        Assert.Equal("custom update", result.UpdateCommand);
        Assert.Equal("custom uninstall", result.UninstallCommand);
    }

    private static AdapterLifecycleCommandResolver Resolver(
        IRuntimeEnvironment? environment = null)
        => new(environment: environment);

    private static AdapterManifest Manifest(
        string name,
        string? npm = null,
        string? brew = null)
        => new()
        {
            SdkVersion = 2,
            Name = name,
            DisplayName = name,
            Description = "test",
            Accent = "#ffffff",
            Binary = name,
            Version = "1.0.0",
            Methods = new Dictionary<string, AdapterMethod>(),
            PackageIdentity = npm is null && brew is null
                ? null
                : new AdapterPackageIdentity { Npm = npm, Brew = brew },
        };

    private static PlatformRecipes Recipes(string command) => new()
    {
        Macos = new InstallRecipe { Cmd = command },
        Linux = new InstallRecipe { Cmd = command },
        Windows = new InstallRecipe { Cmd = command },
    };

    private static string NewDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "cove-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class LifecycleRuntimeEnvironment : IRuntimeEnvironment
    {
        public bool IsWindows { get; init; }
        public bool IsMacOS { get; init; }
        public bool IsLinux { get; init; }
        public string? ExecutablePath { get; init; }
        public string? UserExecutablePath { get; init; }
        public string? MachineExecutablePath { get; init; }
        public string? PathExtensions { get; init; }
        public string HomeDirectory { get; init; } = "/home/test";
        public string SystemDirectory { get; init; } = "/system32";
        public IReadOnlyList<string> WindowsGitRoots { get; init; } = [];
        public IReadOnlyDictionary<string, string?> Variables { get; init; } = new Dictionary<string, string?>();
        public string? GetEnvironmentVariable(string name) => Variables.GetValueOrDefault(name);
    }
}
