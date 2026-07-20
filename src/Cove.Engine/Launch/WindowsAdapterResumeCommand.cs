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
            "omp" => new List<string>
            {
                "--resume",
                sessionId,
                "--allow-home",
                "--hook",
                Path.Combine(adapterDirectory, "cove-hooks.ts"),
            },
            "claude-code" => ClaudeArgs(adapterDirectory, sessionId, overrides.Yolo),
            "codex" => CodexArgs(sessionId, overrides.Yolo),
            _ => null,
        };
        return args is null
            ? null
            : new ResumeCommand(binary, args, overrides.WorkingDir ?? "");
    }

    private static List<string> ClaudeArgs(string adapterDirectory, string sessionId, bool yolo)
    {
        var args = new List<string> { "--resume", sessionId };
        var settings = Path.Combine(adapterDirectory, "hooks-settings.json");
        if (File.Exists(settings))
        {
            args.Add("--settings");
            args.Add(settings);
        }
        if (yolo)
            args.Add("--dangerously-skip-permissions");
        return args;
    }

    private static List<string> CodexArgs(string sessionId, bool yolo)
    {
        var args = new List<string> { "--dangerously-bypass-hook-trust" };
        if (yolo)
            args.Add("--yolo");
        args.Add("resume");
        args.Add(sessionId);
        return args;
    }
}
