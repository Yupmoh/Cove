using Cove.Protocol;

namespace Cove.Adapters;

public sealed record CoveEnvContext(
    string PaneId,
    string CliPath,
    string DataDir,
    string WorkspaceId,
    int HookPort,
    string? TaskId = null,
    string? TaskRunId = null);

public sealed class EnvPrecedenceResolver
{
    private static readonly HashSet<string> NonOverridable = new()
    {
        "COVE", "COVE_CLI_PATH", "COVE_DATA_DIR", "COVE_PANE_ID",
        "COVE_WORKSPACE_ID", "COVE_HOOK_PORT", "COVE_TASK_ID", "COVE_TASK_RUN_ID",
        "COVE_ROOM_ID", "COVE_SDK_VERSION", "COVE_ADAPTER_DIR", "COVE_EVENT"
    };

    private readonly IReadOnlyDictionary<string, string> _systemEnv;

    public EnvPrecedenceResolver(IReadOnlyDictionary<string, string> systemEnv)
    {
        _systemEnv = systemEnv;
    }

    public Dictionary<string, string> Resolve(
        string adapter,
        IReadOnlyList<AdapterEnvVar> adapterEnv,
        IReadOnlyDictionary<string, string>? launcherFlags = null,
        CoveEnvContext? coveContext = null)
    {
        var result = new Dictionary<string, string>();

        foreach (var kv in _systemEnv)
        {
            if (!NonOverridable.Contains(kv.Key))
                result[kv.Key] = kv.Value;
        }

        foreach (var entry in adapterEnv)
        {
            if (!entry.Enabled)
                continue;
            if (!NonOverridable.Contains(entry.Key))
                result[entry.Key] = entry.Value;
        }

        if (launcherFlags is not null)
        {
            foreach (var kv in launcherFlags)
            {
                if (!NonOverridable.Contains(kv.Key))
                    result[kv.Key] = kv.Value;
            }
        }

        InjectCoveVars(result, coveContext);

        return result;
    }

    private static void InjectCoveVars(Dictionary<string, string> env, CoveEnvContext? ctx)
    {
        env["COVE"] = "1";
        if (ctx is null)
            return;

        env["COVE_PANE_ID"] = ctx.PaneId;
        env["COVE_CLI_PATH"] = ctx.CliPath;
        env["COVE_DATA_DIR"] = ctx.DataDir;
        env["COVE_WORKSPACE_ID"] = ctx.WorkspaceId;
        env["COVE_HOOK_PORT"] = ctx.HookPort.ToString();

        if (ctx.TaskId is not null)
            env["COVE_TASK_ID"] = ctx.TaskId;
        if (ctx.TaskRunId is not null)
            env["COVE_TASK_RUN_ID"] = ctx.TaskRunId;
    }

    public static bool IsNonOverridable(string key) => NonOverridable.Contains(key);
}
