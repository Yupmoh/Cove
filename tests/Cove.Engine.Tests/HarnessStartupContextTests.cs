using System.Text.Json;
using Cove.Engine.Launch;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HarnessStartupContextTests
{
    [Theory]
    [InlineData("claude-code", "--append-system-prompt-file")]
    [InlineData("omp", "--append-system-prompt")]
    [InlineData("pi", "--append-system-prompt")]
    public void Apply_AddsNativeSkillFileArgument(string adapter, string flag)
    {
        using var fixture = new Fixture();
        var environment = fixture.Environment();

        var command = fixture.Context.Apply(adapter, adapter, ["resume", "session-1"], environment);

        Assert.Equal(adapter, command.Command);
        Assert.Equal([flag, fixture.SkillPath, "resume", "session-1"], command.Args);
    }

    [Fact]
    public void Apply_AddsCodexDeveloperInstructionsBeforeSubcommand()
    {
        using var fixture = new Fixture("Cove says \"hello\".\nUse C:\\work.");
        var environment = fixture.Environment();

        var command = fixture.Context.Apply("codex", "codex", ["resume", "session-1"], environment);

        Assert.Equal("codex", command.Command);
        Assert.Equal("-c", command.Args[0]);
        Assert.Equal("developer_instructions=\"Cove says \\u0022hello\\u0022.\\nUse C:\\\\work.\"", command.Args[1]);
        Assert.Equal(["resume", "session-1"], command.Args[2..]);
    }

    [Fact]
    public void Apply_InsertsCodexInstructionsAfterWindowsCommandShim()
    {
        using var fixture = new Fixture("Cove control");
        var environment = fixture.Environment();

        var command = fixture.Context.Apply(
            "codex",
            "cmd.exe",
            ["/d", "/s", "/c", @"C:\Users\test\npm\codex.cmd", "resume", "session-1"],
            environment);

        Assert.Equal("cmd.exe", command.Command);
        Assert.Equal(
            ["/d", "/s", "/c", @"C:\Users\test\npm\codex.cmd", "-c", "developer_instructions=\"Cove control\"", "resume", "session-1"],
            command.Args);
    }

    [Fact]
    public void Apply_PreloadsHermesSkill()
    {
        using var fixture = new Fixture();
        var environment = fixture.Environment();

        var command = fixture.Context.Apply("hermes", "hermes", ["--resume", "session-1"], environment);

        Assert.Equal(["--skills", "cove", "--resume", "session-1"], command.Args);
    }

    [Fact]
    public void Apply_MergesOpenCodeInstructionsWithoutDiscardingInlineConfig()
    {
        using var fixture = new Fixture();
        var environment = fixture.Environment();
        environment["OPENCODE_CONFIG_CONTENT"] = """
            {"model":"openai/gpt-5","instructions":["AGENTS.md"]}
            """;

        var command = fixture.Context.Apply("opencode", "opencode", [], environment);

        Assert.Empty(command.Args);
        using var document = JsonDocument.Parse(environment["OPENCODE_CONFIG_CONTENT"]);
        Assert.Equal("openai/gpt-5", document.RootElement.GetProperty("model").GetString());
        Assert.Equal(
            ["AGENTS.md", fixture.SkillPath],
            document.RootElement.GetProperty("instructions")
                .EnumerateArray()
                .Select(value => value.GetString()));
    }

    [Fact]
    public void Apply_MergesCursorSessionHookAndPreservesExistingHooks()
    {
        using var fixture = new Fixture();
        var cursorDirectory = Path.Combine(fixture.Home, ".cursor");
        Directory.CreateDirectory(cursorDirectory);
        File.WriteAllText(
            Path.Combine(cursorDirectory, "hooks.json"),
            """
            {"version":1,"hooks":{"afterFileEdit":[{"command":"format"}]}}
            """);
        var environment = fixture.Environment();

        fixture.Context.Apply("cursor-agent", "cursor-agent", [], environment);

        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(cursorDirectory, "hooks.json")));
        Assert.Equal(
            "format",
            document.RootElement.GetProperty("hooks")
                .GetProperty("afterFileEdit")[0]
                .GetProperty("command").GetString());
        var sessionHook = Assert.Single(
            document.RootElement.GetProperty("hooks")
                .GetProperty("sessionStart")
                .EnumerateArray());
        Assert.Contains(fixture.CliPath, sessionHook.GetProperty("command").GetString());
        Assert.Contains("hook context --adapter cursor-agent", sessionHook.GetProperty("command").GetString());
    }

    [Fact]
    public void Apply_ConfiguresOpenClawBootstrapWithoutReplacingUserConfig()
    {
        using var fixture = new Fixture();
        var openClawDirectory = Path.Combine(fixture.Home, ".openclaw");
        Directory.CreateDirectory(openClawDirectory);
        var userConfig = Path.Combine(openClawDirectory, "openclaw.json");
        File.WriteAllText(userConfig, "{ gateway: { port: 18789 } }");
        var environment = fixture.Environment();

        fixture.Context.Apply("openclaw", "openclaw", [], environment);

        Assert.Equal("{ gateway: { port: 18789 } }", File.ReadAllText(userConfig));
        var managedConfig = environment["OPENCLAW_CONFIG_PATH"];
        var managedContents = File.ReadAllText(managedConfig);
        Assert.Contains("$include", managedContents);
        Assert.Contains("cove-bootstrap", managedContents);
        var hookDirectory = Path.Combine(openClawDirectory, "hooks", "cove-bootstrap");
        Assert.Contains("agent:bootstrap", File.ReadAllText(Path.Combine(hookDirectory, "HOOK.md")));
        var handler = File.ReadAllText(Path.Combine(hookDirectory, "handler.js"));
        Assert.Contains("COVE_SKILL_PATH", handler);
        Assert.Contains("bootstrapFiles", handler);
    }

    [Fact]
    public void Apply_IsIdempotentForArgumentsEnvironmentAndManagedFiles()
    {
        using var fixture = new Fixture();
        foreach (var adapter in new[] { "claude-code", "codex", "omp", "pi", "hermes", "opencode", "cursor-agent", "openclaw" })
        {
            var environment = fixture.Environment();
            var first = fixture.Context.Apply(adapter, adapter, ["resume", "session-1"], environment);
            var firstEnvironment = environment.OrderBy(pair => pair.Key).ToArray();
            var second = fixture.Context.Apply(adapter, first.Command, first.Args, environment);

            Assert.Equal(first.Command, second.Command);
            Assert.Equal(first.Args, second.Args);
            Assert.Equal(firstEnvironment, environment.OrderBy(pair => pair.Key).ToArray());
        }
    }

    [Fact]
    public void Apply_LeavesUnknownAdaptersUnchanged()
    {
        using var fixture = new Fixture();
        var environment = fixture.Environment();

        var command = fixture.Context.Apply("shell", "fish", ["-l"], environment);

        Assert.Equal("fish", command.Command);
        Assert.Equal(["-l"], command.Args);
        Assert.DoesNotContain("OPENCODE_CONFIG_CONTENT", environment.Keys);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(string skill = "Cove control skill")
        {
            Home = Path.Combine(Path.GetTempPath(), "cove-bootstrap-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Home);
            SkillPath = Path.Combine(Home, "data", "adapters", "cove", "skill.md");
            Directory.CreateDirectory(Path.GetDirectoryName(SkillPath)!);
            File.WriteAllText(SkillPath, skill);
            CliPath = Path.Combine(Home, "data", "bin", OperatingSystem.IsWindows() ? "cove.exe" : "cove");
            Directory.CreateDirectory(Path.GetDirectoryName(CliPath)!);
            File.WriteAllText(CliPath, "");
            Context = new HarnessStartupContext(Home, NullLogger.Instance);
        }

        public string Home { get; }
        public string SkillPath { get; }
        public string CliPath { get; }
        public HarnessStartupContext Context { get; }

        public Dictionary<string, string> Environment() => new(StringComparer.Ordinal)
        {
            ["COVE"] = "1",
            ["COVE_NOOK_ID"] = "nook-1",
            ["COVE_SKILL_PATH"] = SkillPath,
            ["COVE_CLI_PATH"] = CliPath,
        };

        public void Dispose()
        {
            if (Directory.Exists(Home))
                Directory.Delete(Home, true);
        }
    }
}
