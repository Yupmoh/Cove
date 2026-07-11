using System.Collections.Generic;

namespace Cove.Engine;

public sealed class SpawnEnvironment
{
    private readonly string _probedPath;
    private readonly string _dataDir;
    private readonly string _cliPath;
    private readonly string _bayId;

    public SpawnEnvironment(string probedPath, string dataDir, string cliPath, string bayId)
    {
        _probedPath = probedPath;
        _dataDir = dataDir;
        _cliPath = cliPath;
        _bayId = bayId;
    }

    public Dictionary<string, string> Build(string nookId, IReadOnlyDictionary<string, string>? callerEnv)
    {
        var env = new Dictionary<string, string>(System.StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry e in System.Environment.GetEnvironmentVariables())
        {
            if (e.Key is string k && e.Value is string v)
                env[k] = v;
        }
        env["PATH"] = _probedPath;
        if (callerEnv is not null)
        {
            foreach (var kv in callerEnv)
                env[kv.Key] = kv.Value;
        }
        env["COVE"] = "1";
        env["COVE_CLI_PATH"] = _cliPath;
        env["COVE_DATA_DIR"] = _dataDir;
        env["COVE_NOOK_ID"] = nookId;
        env["COVE_BAY_ID"] = _bayId;
        env["COVE_TASK_ID"] = "";
        env["COVE_TASK_RUN_ID"] = "";
        env["COVE_HOOK_PORT"] = "";
        return env;
    }
}
