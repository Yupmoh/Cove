using Cove.Engine.Restart;

namespace Cove.Engine.Launch;

internal static class WindowsAdapterResumeCommand
{
    public static ResumeCommand? Build(
        string adapter,
        string binary,
        string adapterDirectory,
        string sessionId,
        LauncherOverrides overrides)
    {
        var args = adapter switch
        {
            "omp" => OmpArgs(adapterDirectory, sessionId, overrides),
            "claude-code" => ClaudeArgs(
                adapterDirectory,
                sessionId,
                overrides),
            "codex" => CodexArgs(sessionId, overrides),
            _ => null,
        };
        return args is null
            ? null
            : new ResumeCommand(binary, args, overrides.WorkingDir ?? "");
    }

    private static List<string> ClaudeArgs(
        string adapterDirectory,
        string sessionId,
        LauncherOverrides overrides)
    {
        var args = new List<string> { "--resume", sessionId };
        var settings = Path.Combine(adapterDirectory, "hooks-settings.json");
        if (File.Exists(settings))
        {
            args.Add("--settings");
            args.Add(settings);
        }
        if (overrides.Yolo)
            args.Add("--dangerously-skip-permissions");
        AddSelections(args, "claude-code", overrides);
        return args;
    }

    private static List<string> CodexArgs(
        string sessionId,
        LauncherOverrides overrides)
    {
        var args = new List<string>
        {
            "--dangerously-bypass-hook-trust",
        };
        if (overrides.Yolo)
            args.Add("--yolo");
        AddSelections(args, "codex", overrides);
        args.Add("resume");
        args.Add(sessionId);
        return args;
    }
    
    private static List<string> OmpArgs(
        string adapterDirectory,
        string sessionId,
        LauncherOverrides overrides)
    {
        var args = new List<string>
        {
            "--resume",
            sessionId,
            "--allow-home",
            "--hook",
            Path.Combine(adapterDirectory, "cove-hooks.ts"),
        };
        AddSelections(args, "omp", overrides);
        return args;
    }

    private static void AddSelections(
        List<string> args,
        string adapter,
        LauncherOverrides overrides)
    {
        var model = Selection(overrides.Model);
        var effort = Selection(overrides.Effort);
        if (model is not null)
        {
            args.Add("--model");
            args.Add(model);
        }
        if (effort is null)
            return;
        if (adapter == "claude-code")
        {
            args.Add("--effort");
            args.Add(effort);
        }
        else if (adapter == "codex")
        {
            args.Add("--config");
            args.Add($"model_reasoning_effort=\"{effort}\"");
        }
        else
        {
            args.Add("--thinking");
            args.Add(effort);
        }
    }

    private static string? Selection(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || string.Equals(
            value,
            "default",
            StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
}
