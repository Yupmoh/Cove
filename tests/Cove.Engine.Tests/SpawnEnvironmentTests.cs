using System.Collections.Generic;
using Cove.Engine;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SpawnEnvironmentTests
{
    [Fact]
    public void Build_InjectsCoveContract()
    {
        var se = new SpawnEnvironment("/probed/bin:/usr/bin", "/data", "/data/bin/cove", "ws1");
        var env = se.Build("nook-abc", null);

        Assert.Equal("1", env["COVE"]);
        Assert.Equal("nook-abc", env["COVE_NOOK_ID"]);
        Assert.Equal("/data", env["COVE_DATA_DIR"]);
        Assert.Equal("/data/bin/cove", env["COVE_CLI_PATH"]);
        Assert.Equal("ws1", env["COVE_BAY_ID"]);
        Assert.Equal("/probed/bin:/usr/bin", env["PATH"]);
        Assert.True(env.ContainsKey("COVE_TASK_ID") && env["COVE_TASK_ID"] == "");
        Assert.True(env.ContainsKey("COVE_TASK_RUN_ID") && env["COVE_TASK_RUN_ID"] == "");
        Assert.True(env.ContainsKey("COVE_HOOK_PORT") && env["COVE_HOOK_PORT"] == "");
    }

    [Fact]
    public void Build_CoveVarsAreNonOverridable()
    {
        var se = new SpawnEnvironment("/probed/bin:/usr/bin", "/data", "/data/bin/cove", "ws1");
        var env = se.Build("p1", new Dictionary<string, string>
        {
            ["COVE"] = "0",
            ["COVE_NOOK_ID"] = "evil",
            ["MYVAR"] = "x",
        });

        Assert.Equal("1", env["COVE"]);
        Assert.Equal("p1", env["COVE_NOOK_ID"]);
        Assert.Equal("x", env["MYVAR"]);
    }
}
