using System.Collections.Generic;
using Cove.Engine;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SpawnEnvironmentTests
{
    [Fact]
    public void Build_InjectsCoveContract()
    {
        var se = new SpawnEnvironment("/probed/bin:/usr/bin", "/data", "/data/bin/cove", "ws1", "dev");
        var env = se.Build("nook-abc", null);

        Assert.Equal("1", env["COVE"]);
        Assert.Equal("dev", env["COVE_CHANNEL"]);
        Assert.Equal("nook-abc", env["COVE_NOOK_ID"]);
        Assert.Equal("/data", env["COVE_DATA_DIR"]);
        Assert.Equal("/data/bin/cove", env["COVE_CLI_PATH"]);
        Assert.Equal(Path.Combine("/data", "adapters", "cove", "skill.md"), env["COVE_SKILL_PATH"]);
        Assert.Equal("ws1", env["COVE_BAY_ID"]);
        Assert.Equal("/data/bin" + Path.PathSeparator + "/probed/bin:/usr/bin", env["PATH"]);
        Assert.True(env.ContainsKey("COVE_TASK_ID") && env["COVE_TASK_ID"] == "");
        Assert.True(env.ContainsKey("COVE_TASK_RUN_ID") && env["COVE_TASK_RUN_ID"] == "");
        Assert.True(env.ContainsKey("COVE_HOOK_PORT") && env["COVE_HOOK_PORT"] == "");
    }

    [Fact]
    public void Build_CoveVarsAreNonOverridable()
    {
        var se = new SpawnEnvironment("/probed/bin:/usr/bin", "/data", "/data/bin/cove", "ws1", "dev");
        var env = se.Build("p1", new Dictionary<string, string>
        {
            ["COVE"] = "0",
            ["COVE_CHANNEL"] = "stable",
            ["COVE_NOOK_ID"] = "evil",
            ["MYVAR"] = "x",
        });

        Assert.Equal("1", env["COVE"]);
        Assert.Equal("dev", env["COVE_CHANNEL"]);
        Assert.Equal("p1", env["COVE_NOOK_ID"]);
        Assert.Equal("x", env["MYVAR"]);
    }

    [Fact]
    public void ApplyTerminalIdentity_ScrubsHostOnlyAndHarnessVars()
    {
        var env = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            ["TERM"] = "dumb",
            ["NO_COLOR"] = "1",
            ["CI"] = "1",
            ["TERMINFO"] = "/Applications/Ghostty.app/Contents/Resources/terminfo",
            ["FORCE_COLOR"] = "0",
            ["CLAUDECODE"] = "1",
            ["CLAUDE_CODE_ENTRYPOINT"] = "cli",
            ["OMPCODE"] = "1",
            ["GHOSTTY_RESOURCES_DIR"] = "/x",
            ["HERDR_SESSION"] = "abc",
            ["HOME"] = "/Users/x",
        };

        SpawnEnvironment.ApplyTerminalIdentity(env);

        Assert.Equal("xterm-256color", env["TERM"]);
        Assert.Equal("truecolor", env["COLORTERM"]);
        Assert.Equal("Cove", env["TERM_PROGRAM"]);
        Assert.False(env.ContainsKey("NO_COLOR"));
        Assert.False(env.ContainsKey("TERMINFO"));
        Assert.False(env.ContainsKey("CI"));
        Assert.False(env.ContainsKey("FORCE_COLOR"));
        Assert.False(env.ContainsKey("CLAUDECODE"));
        Assert.False(env.ContainsKey("CLAUDE_CODE_ENTRYPOINT"));
        Assert.False(env.ContainsKey("OMPCODE"));
        Assert.False(env.ContainsKey("GHOSTTY_RESOURCES_DIR"));
        Assert.False(env.ContainsKey("HERDR_SESSION"));
        Assert.Equal("/Users/x", env["HOME"]);
    }

    [Fact]
    public void Build_CallerEnvOverridesTerminalIdentity()
    {
        var se = new SpawnEnvironment("/probed/bin:/usr/bin", "/data", "/data/bin/cove", "ws1", "dev");
        var env = se.Build("p1", new Dictionary<string, string> { ["TERM"] = "xterm-kitty" });

        Assert.Equal("xterm-kitty", env["TERM"]);
    }

    [Fact]
    public void Build_EstablishesCoveTerminalIdentity()
    {
        var se = new SpawnEnvironment("/probed/bin:/usr/bin", "/data", "/data/bin/cove", "ws1", "dev");
        var env = se.Build("p1", null);

        Assert.Equal("xterm-256color", env["TERM"]);
        Assert.Equal("truecolor", env["COLORTERM"]);
        Assert.Equal("Cove", env["TERM_PROGRAM"]);
        Assert.False(env.ContainsKey("NO_COLOR"));
        Assert.False(env.ContainsKey("CI"));
    }
}
