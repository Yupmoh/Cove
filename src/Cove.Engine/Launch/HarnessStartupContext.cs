using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Launch;

public sealed record HarnessStartupCommand(string Command, string[] Args);

public sealed class HarnessStartupContext
{
    private const string CoveOpenClawBaseConfig = "COVE_OPENCLAW_BASE_CONFIG";
    private static readonly string[] CodexManagedEnvironmentKeys =
    [
        "COVE",
        "COVE_CHANNEL",
        "COVE_CLI_PATH",
        "COVE_DATA_DIR",
        "COVE_NOOK_ID",
        "COVE_NOOK_TOKEN",
        "COVE_BAY_ID",
        "COVE_SHORE_ID",
        "COVE_HOOK_PORT",
        "COVE_SKILL_PATH",
        "COVE_ADAPTER_DIR",
        "COVE_TASK_ID",
        "COVE_TASK_RUN_ID",
    ];
    private static readonly string[] CodexRequiredEnvironmentKeys =
    [
        "COVE_CHANNEL",
        "COVE_CLI_PATH",
        "COVE_NOOK_ID",
        "COVE_NOOK_TOKEN",
    ];
    private readonly string _homeDirectory;
    private readonly ILogger _logger;

    public HarnessStartupContext(string homeDirectory, ILogger logger)
    {
        _homeDirectory = homeDirectory;
        _logger = logger;
    }

    public HarnessStartupCommand Apply(
        string adapter,
        string command,
        IReadOnlyList<string> args,
        Dictionary<string, string> environment)
    {
        if (!KnownAdapter(adapter))
            return new HarnessStartupCommand(command, args.ToArray());
        if (string.Equals(adapter, "codex", StringComparison.Ordinal))
            ValidateCodexEnvironment(environment);

        var hasSkill = environment.TryGetValue("COVE_SKILL_PATH", out var skillPath)
            && !string.IsNullOrWhiteSpace(skillPath)
            && File.Exists(skillPath);
        if (!hasSkill)
        {
            _logger.HarnessSkillMissing(adapter, skillPath ?? "");
            return string.Equals(adapter, "codex", StringComparison.Ordinal)
                ? ConfigureCodex(command, args, environment, null)
                : new HarnessStartupCommand(command, args.ToArray());
        }

        try
        {
            return adapter switch
            {
                "claude-code" => InsertFlag(command, args, "--append-system-prompt-file", skillPath!),
                "codex" => ConfigureCodex(command, args, environment, File.ReadAllText(skillPath!)),
                "omp" or "pi" => InsertFlag(command, args, "--append-system-prompt", skillPath!),
                "hermes" => InsertFlag(command, args, "--skills", "cove"),
                "opencode" => ConfigureOpenCode(command, args, environment, skillPath!),
                "cursor-agent" => ConfigureCursor(command, args, environment),
                "openclaw" => ConfigureOpenClaw(command, args, environment),
                _ => new HarnessStartupCommand(command, args.ToArray()),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            _logger.HarnessBootstrapFailed(adapter, ex.Message);
            return new HarnessStartupCommand(command, args.ToArray());
        }
    }

    private static bool KnownAdapter(string adapter) => adapter is
        "claude-code" or "codex" or "omp" or "pi" or "hermes" or "opencode" or "cursor-agent" or "openclaw";

    private static HarnessStartupCommand InsertFlag(
        string command,
        IReadOnlyList<string> args,
        string flag,
        string value)
    {
        var existing = FlagIndex(args, flag);
        if (existing >= 0 && existing + 1 < args.Count && string.Equals(args[existing + 1], value, StringComparison.Ordinal))
            return new HarnessStartupCommand(command, args.ToArray());

        var insertion = CommandShimInsertion(command, args);
        var result = new string[args.Count + 2];
        for (var index = 0; index < insertion; index++)
            result[index] = args[index];
        result[insertion] = flag;
        result[insertion + 1] = value;
        for (var index = insertion; index < args.Count; index++)
            result[index + 2] = args[index];
        return new HarnessStartupCommand(command, result);
    }

    private static int FlagIndex(IReadOnlyList<string> args, string flag)
    {
        for (var index = 0; index < args.Count; index++)
            if (string.Equals(args[index], flag, StringComparison.Ordinal))
                return index;
        return -1;
    }

    private static int CommandShimInsertion(string command, IReadOnlyList<string> args)
    {
        if (!string.Equals(Path.GetFileName(command), "cmd.exe", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (args.Count < 4
            || !string.Equals(args[0], "/d", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(args[1], "/s", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(args[2], "/c", StringComparison.OrdinalIgnoreCase))
            return 0;
        return 4;
    }

    private static string CodexInstructions(string skill)
    {
        var encoded = JsonEncodedText.Encode(skill, JavaScriptEncoder.Default).ToString();
        return "developer_instructions=\"" + encoded + "\"";
    }
    private static HarnessStartupCommand ConfigureCodex(
        string command,
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string> environment,
        string? skill)
    {
        var configured = skill is null
            ? new HarnessStartupCommand(command, args.ToArray())
            : InsertFlag(command, args, "-c", CodexInstructions(skill));
        foreach (var key in CodexManagedEnvironmentKeys)
        {
            if (!environment.TryGetValue(key, out var value))
                continue;
            if ((key == "COVE_TASK_ID" || key == "COVE_TASK_RUN_ID")
                && string.IsNullOrEmpty(value))
                continue;
            configured = InsertCodexConfig(
                configured.Command,
                configured.Args,
                "shell_environment_policy.set." + key + "=" + TomlBasicString(value));
        }
        return configured;
    }

    private static void ValidateCodexEnvironment(IReadOnlyDictionary<string, string> environment)
    {
        foreach (var key in CodexRequiredEnvironmentKeys)
            if (!environment.TryGetValue(key, out var value)
                || string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("managed Codex environment missing " + key);
    }

    private static HarnessStartupCommand InsertCodexConfig(
        string command,
        IReadOnlyList<string> args,
        string value)
    {
        for (var index = 0; index + 1 < args.Count; index++)
            if (string.Equals(args[index], "-c", StringComparison.Ordinal)
                && string.Equals(args[index + 1], value, StringComparison.Ordinal))
                return new HarnessStartupCommand(command, args.ToArray());

        var insertion = args.Count;
        var commandStart = CommandShimInsertion(command, args);
        for (var index = commandStart; index < args.Count; index++)
        {
            if (!string.Equals(args[index], "resume", StringComparison.Ordinal))
                continue;
            insertion = index;
            break;
        }
        var result = new string[args.Count + 2];
        for (var index = 0; index < insertion; index++)
            result[index] = args[index];
        result[insertion] = "-c";
        result[insertion + 1] = value;
        for (var index = insertion; index < args.Count; index++)
            result[index + 2] = args[index];
        return new HarnessStartupCommand(command, result);
    }

    private static string TomlBasicString(string value)
    {
        const string hex = "0123456789ABCDEF";
        var result = new StringBuilder(value.Length + 2);
        result.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\': result.Append("\\\\"); break;
                case '"': result.Append("\\\""); break;
                case '\b': result.Append("\\b"); break;
                case '\t': result.Append("\\t"); break;
                case '\n': result.Append("\\n"); break;
                case '\f': result.Append("\\f"); break;
                case '\r': result.Append("\\r"); break;
                default:
                    if (character < 0x20 || character == 0x7F)
                    {
                        result.Append("\\u");
                        result.Append(hex[(character >> 12) & 0xF]);
                        result.Append(hex[(character >> 8) & 0xF]);
                        result.Append(hex[(character >> 4) & 0xF]);
                        result.Append(hex[character & 0xF]);
                    }
                    else
                    {
                        result.Append(character);
                    }
                    break;
            }
        }
        return result.Append('"').ToString();
    }

    private HarnessStartupCommand ConfigureOpenCode(
        string command,
        IReadOnlyList<string> args,
        Dictionary<string, string> environment,
        string skillPath)
    {
        JsonObject root;
        if (environment.TryGetValue("OPENCODE_CONFIG_CONTENT", out var existing)
            && !string.IsNullOrWhiteSpace(existing))
        {
            root = JsonNode.Parse(
                existing,
                documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                }) as JsonObject
                ?? throw new JsonException("OPENCODE_CONFIG_CONTENT must be an object");
        }
        else
        {
            root = new JsonObject();
        }

        JsonArray instructions;
        if (root["instructions"] is null)
        {
            instructions = [];
            root["instructions"] = instructions;
        }
        else
        {
            instructions = root["instructions"] as JsonArray
                ?? throw new JsonException("OPENCODE_CONFIG_CONTENT instructions must be an array");
        }
        if (!instructions.Any(node => string.Equals(node?.GetValue<string>(), skillPath, StringComparison.Ordinal)))
            instructions.Insert(instructions.Count, StringNode(skillPath));
        environment["OPENCODE_CONFIG_CONTENT"] = root.ToJsonString();
        return new HarnessStartupCommand(command, args.ToArray());
    }

    private HarnessStartupCommand ConfigureCursor(
        string command,
        IReadOnlyList<string> args,
        Dictionary<string, string> environment)
    {
        if (!environment.TryGetValue("COVE_CLI_PATH", out var cliPath)
            || string.IsNullOrWhiteSpace(cliPath))
            throw new InvalidOperationException("COVE_CLI_PATH is required for Cursor context bootstrap");
        var cursorDirectory = Path.Combine(_homeDirectory, ".cursor");
        var hooksPath = Path.Combine(cursorDirectory, "hooks.json");
        Directory.CreateDirectory(cursorDirectory);
        JsonObject root;
        if (File.Exists(hooksPath))
        {
            root = JsonNode.Parse(
                File.ReadAllText(hooksPath),
                documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                }) as JsonObject
                ?? throw new JsonException("Cursor hooks config must be an object");
        }
        else
        {
            root = new JsonObject();
        }
        root["version"] ??= 1;
        var hooks = root["hooks"] as JsonObject;
        if (hooks is null)
        {
            hooks = new JsonObject();
            root["hooks"] = hooks;
        }
        var sessionStart = hooks["sessionStart"] as JsonArray;
        if (sessionStart is null)
        {
            sessionStart = [];
            hooks["sessionStart"] = sessionStart;
        }
        var hookCommand = QuoteCommand(cliPath) + " hook context --adapter cursor-agent";
        if (!sessionStart.Any(node => string.Equals(node?["command"]?.GetValue<string>(), hookCommand, StringComparison.Ordinal)))
            sessionStart.Insert(sessionStart.Count, new JsonObject { ["command"] = hookCommand });
        WriteIfChanged(hooksPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return new HarnessStartupCommand(command, args.ToArray());
    }

    private HarnessStartupCommand ConfigureOpenClaw(
        string command,
        IReadOnlyList<string> args,
        Dictionary<string, string> environment)
    {
        var stateDirectory = environment.TryGetValue("OPENCLAW_STATE_DIR", out var configuredState)
            && !string.IsNullOrWhiteSpace(configuredState)
                ? configuredState
                : Path.Combine(_homeDirectory, ".openclaw");
        var hookDirectory = Path.Combine(stateDirectory, "hooks", "cove-bootstrap");
        Directory.CreateDirectory(hookDirectory);
        WriteIfChanged(Path.Combine(hookDirectory, "HOOK.md"), OpenClawHookManifest);
        WriteIfChanged(Path.Combine(hookDirectory, "handler.js"), OpenClawHookHandler);

        if (!environment.TryGetValue(CoveOpenClawBaseConfig, out var baseConfig))
        {
            baseConfig = environment.TryGetValue("OPENCLAW_CONFIG_PATH", out var configuredPath)
                && !string.IsNullOrWhiteSpace(configuredPath)
                    ? configuredPath
                    : Path.Combine(stateDirectory, "openclaw.json");
            environment[CoveOpenClawBaseConfig] = baseConfig;
        }
        var configDirectory = Path.GetDirectoryName(baseConfig)
            ?? stateDirectory;
        Directory.CreateDirectory(configDirectory);
        var managedConfig = Path.Combine(configDirectory, "cove.openclaw.json5");
        var include = File.Exists(baseConfig)
            ? "  \"$include\": \"" + JsonEncodedText.Encode(Path.GetFileName(baseConfig)).ToString() + "\",\n"
            : "";
        var config = "{\n" + include + "  \"hooks\": { \"internal\": { \"entries\": { \"cove-bootstrap\": { \"enabled\": true } } } }\n}\n";
        WriteIfChanged(managedConfig, config);
        environment["OPENCLAW_CONFIG_PATH"] = managedConfig;
        return new HarnessStartupCommand(command, args.ToArray());
    }

    private static JsonNode StringNode(string value) =>
        JsonNode.Parse("\"" + JsonEncodedText.Encode(value).ToString() + "\"")
        ?? throw new JsonException("string node serialization failed");

    private static string QuoteCommand(string path) =>
        path.Any(char.IsWhiteSpace) || path.Contains('"')
            ? "\"" + path.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : path;

    private static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path) && string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal))
            return;
        File.WriteAllText(path, content);
    }

    private const string OpenClawHookManifest = """
        ---
        name: cove-bootstrap
        description: Inject Cove control context into Cove-managed OpenClaw sessions
        metadata:
          { "openclaw": { "events": ["agent:bootstrap"], "always": true } }
        ---

        # Cove bootstrap
        """;

    private const string OpenClawHookHandler = """
        import { readFile } from "node:fs/promises";

        export default async function handler(event) {
          const skillPath = process.env.COVE_SKILL_PATH;
          if (event.type !== "agent" || event.action !== "bootstrap" || !process.env.COVE_NOOK_ID || !skillPath) return;
          const content = await readFile(skillPath, "utf8");
          if (event.context.bootstrapFiles.some((file) => file.path === skillPath)) return;
          event.context.bootstrapFiles.push({ name: "AGENTS.md", path: skillPath, content, missing: false });
        }
        """;
}

internal static partial class HarnessStartupContextLog
{
    [ZLoggerMessage(LogLevel.Warning, "harness startup context missing skill adapter={adapter} path={path}")]
    public static partial void HarnessSkillMissing(this ILogger logger, string adapter, string path);

    [ZLoggerMessage(LogLevel.Warning, "harness startup context failed adapter={adapter} error={error}")]
    public static partial void HarnessBootstrapFailed(this ILogger logger, string adapter, string error);
}
