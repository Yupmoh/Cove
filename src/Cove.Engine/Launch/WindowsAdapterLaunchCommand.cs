using Cove.Adapters;
using Cove.Engine.Restart;

namespace Cove.Engine.Launch;

internal static class WindowsAdapterLaunchCommand
{
    public static ResumeCommand? Build(
        string binary,
        string adapterDirectory,
        LaunchProfile profile,
        LauncherOverrides overrides)
    {
        var args = profile.Adapter switch
        {
            "omp" => OmpArgs(adapterDirectory),
            "claude-code" => ClaudeArgs(adapterDirectory, profile, overrides),
            "codex" => CodexArgs(overrides.Yolo),
            _ => null,
        };
        if (args is null)
            return null;

        if (profile.CliArgs.Count > 1)
            args.AddRange(profile.CliArgs.Skip(1));
        args.AddRange(overrides.ExtraFlags);
        return new ResumeCommand(binary, args, overrides.WorkingDir ?? "");
    }

    private static List<string> OmpArgs(string adapterDirectory)
        =>
        [
            "--allow-home",
            "--hook",
            Path.Combine(adapterDirectory, "cove-hooks.ts"),
        ];

    private static List<string> ClaudeArgs(
        string adapterDirectory,
        LaunchProfile profile,
        LauncherOverrides overrides)
    {
        var args = new List<string>();
        var settings = Path.Combine(adapterDirectory, "hooks-settings.json");
        if (File.Exists(settings))
        {
            args.Add("--settings");
            args.Add(settings);
        }
        if (overrides.Yolo)
            args.Add("--dangerously-skip-permissions");
        if (!string.IsNullOrWhiteSpace(profile.Model)
            && !string.Equals(profile.Model, "default", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--model");
            args.Add(profile.Model);
        }
        return args;
    }

    private static List<string> CodexArgs(bool yolo)
    {
        var args = new List<string> { "--dangerously-bypass-hook-trust" };
        if (yolo)
            args.Add("--yolo");
        return args;
    }
}
