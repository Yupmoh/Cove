using Cove.Adapters;
using Cove.Protocol;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class EnvPrecedenceResolverTests
{
    private static readonly string[] CoveNonOverridable =
        { "COVE", "COVE_CLI_PATH", "COVE_DATA_DIR", "COVE_PANE_ID", "COVE_WORKSPACE_ID", "COVE_HOOK_PORT", "COVE_TASK_ID", "COVE_TASK_RUN_ID" };

    [Fact]
    public void Resolve_SystemEnv_First()
    {
        var systemEnv = new Dictionary<string, string> { ["PATH"] = "/usr/bin" };
        var resolver = new EnvPrecedenceResolver(systemEnv);
        var result = resolver.Resolve("claude-code", Array.Empty<AdapterEnvVar>());
        Assert.Equal("/usr/bin", result["PATH"]);
    }

    [Fact]
    public void Resolve_AdapterEnv_OverridesSystem()
    {
        var systemEnv = new Dictionary<string, string> { ["MY_VAR"] = "system" };
        var adapterEnv = new[] { new AdapterEnvVar("MY_VAR", "adapter") };
        var resolver = new EnvPrecedenceResolver(systemEnv);
        var result = resolver.Resolve("claude-code", adapterEnv);
        Assert.Equal("adapter", result["MY_VAR"]);
    }

    [Fact]
    public void Resolve_LauncherFlags_OverrideAdapter()
    {
        var systemEnv = new Dictionary<string, string> { ["MY_VAR"] = "system" };
        var adapterEnv = new[] { new AdapterEnvVar("MY_VAR", "adapter") };
        var launcherFlags = new Dictionary<string, string> { ["MY_VAR"] = "launcher" };
        var resolver = new EnvPrecedenceResolver(systemEnv);
        var result = resolver.Resolve("claude-code", adapterEnv, launcherFlags);
        Assert.Equal("launcher", result["MY_VAR"]);
    }

    [Fact]
    public void Resolve_CoveNonOverridable_AlwaysWins()
    {
        var systemEnv = new Dictionary<string, string> { ["COVE_PANE_ID"] = "user-tries-to-set" };
        var adapterEnv = new[] { new AdapterEnvVar("COVE_PANE_ID", "adapter-tries") };
        var resolver = new EnvPrecedenceResolver(systemEnv);
        var result = resolver.Resolve("claude-code", adapterEnv, coveContext: new CoveEnvContext(PaneId: "pane-123", CliPath: "/cove", DataDir: "/data", WorkspaceId: "ws1", HookPort: 9999));
        Assert.Equal("pane-123", result["COVE_PANE_ID"]);
    }

    [Fact]
    public void Resolve_CoveNonOverridable_SilentlyDropsUserCollision()
    {
        var systemEnv = new Dictionary<string, string> { ["COVE"] = "0" };
        var resolver = new EnvPrecedenceResolver(systemEnv);
        var result = resolver.Resolve("claude-code", Array.Empty<AdapterEnvVar>(), coveContext: new CoveEnvContext(PaneId: "p1", CliPath: "/cove", DataDir: "/data", WorkspaceId: "ws1", HookPort: 9999));
        Assert.Equal("1", result["COVE"]);
        Assert.Equal("p1", result["COVE_PANE_ID"]);
        Assert.Equal("/cove", result["COVE_CLI_PATH"]);
        Assert.Equal("/data", result["COVE_DATA_DIR"]);
        Assert.Equal("ws1", result["COVE_WORKSPACE_ID"]);
        Assert.Equal("9999", result["COVE_HOOK_PORT"]);
    }

    [Fact]
    public void Resolve_DisabledAdapterVar_NotApplied()
    {
        var adapterEnv = new[] { new AdapterEnvVar("MY_VAR", "adapter", Enabled: false) };
        var resolver = new EnvPrecedenceResolver(new Dictionary<string, string>());
        var result = resolver.Resolve("claude-code", adapterEnv);
        Assert.False(result.ContainsKey("MY_VAR"));
    }

    [Fact]
    public void Resolve_TaskBound_AddsTaskEnv()
    {
        var resolver = new EnvPrecedenceResolver(new Dictionary<string, string>());
        var result = resolver.Resolve("claude-code", Array.Empty<AdapterEnvVar>(), coveContext: new CoveEnvContext(PaneId: "p1", CliPath: "/cove", DataDir: "/data", WorkspaceId: "ws1", HookPort: 9999, TaskId: "task-5", TaskRunId: "run-9"));
        Assert.Equal("task-5", result["COVE_TASK_ID"]);
        Assert.Equal("run-9", result["COVE_TASK_RUN_ID"]);
    }

    [Fact]
    public void Resolve_NoTaskContext_TaskVarsAbsent()
    {
        var resolver = new EnvPrecedenceResolver(new Dictionary<string, string>());
        var result = resolver.Resolve("claude-code", Array.Empty<AdapterEnvVar>(), coveContext: new CoveEnvContext(PaneId: "p1", CliPath: "/cove", DataDir: "/data", WorkspaceId: "ws1", HookPort: 9999));
        Assert.False(result.ContainsKey("COVE_TASK_ID"));
        Assert.False(result.ContainsKey("COVE_TASK_RUN_ID"));
    }
}

