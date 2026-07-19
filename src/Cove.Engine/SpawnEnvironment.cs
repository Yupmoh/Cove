using System.Collections.Generic;

namespace Cove.Engine;

public sealed class SpawnEnvironment
{
    private readonly string _probedPath;
    private readonly string _dataDir;
    private readonly string _cliPath;
    private readonly string _bayId;
    private readonly string _channel;

    public SpawnEnvironment(string probedPath, string dataDir, string cliPath, string bayId, string channel)
    {
        _probedPath = probedPath;
        _dataDir = dataDir;
        _cliPath = cliPath;
        _bayId = bayId;
        _channel = channel;
    }

    private static readonly string[] HostOnlyKeys =
    {
        "NO_COLOR", "CI", "FORCE_COLOR", "TERM_PROGRAM_VERSION", "TERMINFO", "TERMINFO_DIRS",
        "CLAUDECODE", "CLAUDE_CODE_ENTRYPOINT", "OMPCODE",
    };

    private static readonly string[] HostOnlyPrefixes = { "GHOSTTY_", "HERDR_" };

    public static void ApplyTerminalIdentity(Dictionary<string, string> env)
    {
        foreach (var key in HostOnlyKeys)
            env.Remove(key);
        var leaked = new List<string>();
        foreach (var key in env.Keys)
        {
            foreach (var prefix in HostOnlyPrefixes)
            {
                if (key.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    leaked.Add(key);
                    break;
                }
            }
        }
        foreach (var key in leaked)
            env.Remove(key);
        env["TERM"] = "xterm-256color";
        env["COLORTERM"] = "truecolor";
        env["TERM_PROGRAM"] = "Cove";
    }

    public Dictionary<string, string> Build(string nookId, IReadOnlyDictionary<string, string>? callerEnv, string? nookToken = null)
    {
        var env = new Dictionary<string, string>(System.StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry e in System.Environment.GetEnvironmentVariables())
        {
            if (e.Key is string k && e.Value is string v)
                env[k] = v;
        }
        ApplyTerminalIdentity(env);
        env["PATH"] = _probedPath;
        if (callerEnv is not null)
        {
            foreach (var kv in callerEnv)
                env[kv.Key] = kv.Value;
        }
        env["COVE"] = "1";
        env["COVE_CHANNEL"] = _channel;
        env["COVE_CLI_PATH"] = _cliPath;
        env["COVE_DATA_DIR"] = _dataDir;
        env["COVE_NOOK_ID"] = nookId;
        env["COVE_NOOK_TOKEN"] = nookToken ?? "";
        env["COVE_BAY_ID"] = _bayId;
        env["COVE_SHORE_ID"] = "";
        env["COVE_TASK_ID"] = "";
        env["COVE_TASK_RUN_ID"] = "";
        env["COVE_HOOK_PORT"] = "";
        return env;
    }
}
