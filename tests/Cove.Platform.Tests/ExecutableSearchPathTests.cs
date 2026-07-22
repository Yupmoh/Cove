using Xunit;

namespace Cove.Platform.Tests;

public sealed class ExecutableSearchPathTests
{
    [Fact]
    public void WindowsOrdersAllSourcesAndDeduplicatesFirstOccurrence()
    {
        var environment = new SearchPathRuntimeEnvironment
        {
            IsWindows = true,
            HomeDirectory = "/users/test",
            ExecutablePath = "/process-a;/shared",
            UserExecutablePath = "/user-a;/shared",
            MachineExecutablePath = "/machine-a",
            Variables = new Dictionary<string, string?>
            {
                ["NVM_BIN"] = "/nvm",
                ["PNPM_HOME"] = "/pnpm",
                ["VOLTA_HOME"] = "/volta",
                ["FNM_MULTISHELL_PATH"] = "/fnm",
                ["APPDATA"] = "/appdata",
                ["USERPROFILE"] = "/users/test",
                ["LOCALAPPDATA"] = "/localappdata",
            },
        };

        var directories = new ExecutableSearchPath(environment).Resolve(
            "/login-a;/shared",
            ["~/adapter", "/manifest"]);

        Assert.Equal(
            [
                "/login-a",
                "/shared",
                "/process-a",
                "/user-a",
                "/machine-a",
                "/nvm",
                "/pnpm",
                Path.Combine("/volta", "bin"),
                Path.Combine("/fnm", "bin"),
                Path.Combine("/appdata", "npm"),
                Path.Combine("/users/test", ".bun", "bin"),
                Path.Combine("/localappdata", "pnpm"),
                Path.Combine("/users/test", "scoop", "shims"),
                Path.Combine("/localappdata", "Microsoft", "WinGet", "Links"),
                Path.Combine("/users/test", "adapter"),
                "/manifest",
            ],
            directories);
    }

    [Fact]
    public void MacOsOrdersManagedAndFallbackRootsBeforeManifestRoots()
    {
        var environment = new SearchPathRuntimeEnvironment
        {
            IsMacOS = true,
            HomeDirectory = "/Users/test",
            ExecutablePath = "/process:/shared",
            Variables = new Dictionary<string, string?>
            {
                ["NVM_BIN"] = "/nvm",
                ["PNPM_HOME"] = "/pnpm",
                ["VOLTA_HOME"] = "/volta",
                ["FNM_MULTISHELL_PATH"] = "/fnm",
            },
        };

        var directories = new ExecutableSearchPath(environment).Resolve(
            "/login:/shared",
            ["~/.claude/local"]);

        Assert.Equal(
            [
                "/login",
                "/shared",
                "/process",
                "/nvm",
                "/pnpm",
                Path.Combine("/volta", "bin"),
                Path.Combine("/fnm", "bin"),
                Path.Combine("/Users/test", ".bun", "bin"),
                Path.Combine("/Users/test", ".local", "bin"),
                "/opt/homebrew/bin",
                "/usr/local/bin",
                Path.Combine("/Users/test", ".claude", "local"),
            ],
            directories);
    }

    [Fact]
    public void LinuxIncludesPortableFallbacksInExactOrder()
    {
        var environment = new SearchPathRuntimeEnvironment
        {
            IsLinux = true,
            HomeDirectory = "/home/test",
        };

        var directories = new ExecutableSearchPath(environment).Resolve(null, []);

        Assert.Equal(
            [
                Path.Combine("/home/test", ".bun", "bin"),
                Path.Combine("/home/test", ".local", "bin"),
                Path.Combine("/home/test", ".linuxbrew", "bin"),
                "/home/linuxbrew/.linuxbrew/bin",
                "/usr/local/bin",
                "/usr/bin",
            ],
            directories);
    }

    [Fact]
    public void RemovesEmptyEntriesNormalizesTrailingSeparatorsAndExpandsHome()
    {
        var environment = new SearchPathRuntimeEnvironment
        {
            IsMacOS = true,
            HomeDirectory = "/Users/test",
            ExecutablePath = "/same/:/same",
        };

        var directories = new ExecutableSearchPath(environment).Resolve(
            ":/same::",
            ["~/custom/", "", "   "]);

        Assert.Equal(1, directories.Count(path => path == "/same"));
        Assert.Contains(Path.Combine("/Users/test", "custom"), directories);
        Assert.DoesNotContain(directories, string.IsNullOrWhiteSpace);
    }

    [Theory]
    [InlineData("EXE;.Cmd;bat", ".exe", ".cmd", ".bat")]
    [InlineData("", ".exe", ".com", ".cmd", ".bat")]
    [InlineData(null, ".exe", ".com", ".cmd", ".bat")]
    public void WindowsExtensionsFollowNormalizedPathExt(
        string? pathExtensions,
        params string[] expected)
    {
        var environment = new SearchPathRuntimeEnvironment
        {
            IsWindows = true,
            PathExtensions = pathExtensions,
        };

        Assert.Equal(expected, new ExecutableSearchPath(environment).ExecutableExtensions);
    }

    private sealed class SearchPathRuntimeEnvironment : IRuntimeEnvironment
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
}
