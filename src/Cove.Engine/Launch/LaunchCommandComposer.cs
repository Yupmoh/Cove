using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Adapters;
using Cove.Engine.Restart;

namespace Cove.Engine.Launch;

public sealed record LaunchFlags(
    [property: JsonPropertyName("dangerouslySkipPermissions")] bool DangerouslySkipPermissions,
    [property: JsonPropertyName("worktreePath")] string? WorktreePath,
    [property: JsonPropertyName("extra")] IReadOnlyDictionary<string, string> Extra,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("effort")] string? Effort,
    [property: JsonPropertyName("extraArgs")] string? ExtraArgs,
    [property: JsonPropertyName("provider")] string? Provider,
    [property: JsonPropertyName("command")] string? Command);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LaunchFlags))]
public sealed partial class LaunchFlagsJsonContext : JsonSerializerContext
{
}

public sealed class LaunchCommandComposer
{
    public ResumeCommand Compose(LaunchProfile profile, LauncherOverrides overrides)
    {
        var binary = profile.CliArgs.Count > 0 ? profile.CliArgs[0] : "agent";
        return Compose(binary, profile, overrides);
    }

    public ResumeCommand Compose(string binary, LaunchProfile profile, LauncherOverrides overrides)
    {
        var args = new List<string>(
            profile.CliArgs.Count > 0
                ? profile.CliArgs.Skip(1)
                : Array.Empty<string>());
        ApplyOverrides(args, profile, overrides);
        return new ResumeCommand(binary, args, overrides.WorkingDir ?? "");
    }

    public ResumeCommand ParseAdapterCommand(JsonElement json, string? workingDirectory)
    {
        if (!json.TryGetProperty("command", out var commandProperty)
            || commandProperty.ValueKind != JsonValueKind.Array)
        {
            throw new ResumeFailedException(
                "build_launch_command returned invalid command payload");
        }

        var parts = new List<string>();
        foreach (var item in commandProperty.EnumerateArray())
        {
            var value = item.GetString();
            if (value is not null)
                parts.Add(value);
        }

        if (parts.Count == 0)
        {
            throw new ResumeFailedException(
                "build_launch_command returned empty command array");
        }

        return new ResumeCommand(
            parts[0],
            parts.Skip(1).ToArray(),
            workingDirectory ?? "");
    }

    public string BuildFlagsJson(
        LaunchProfile profile,
        LauncherOverrides overrides)
    {
        var extra = new Dictionary<string, string>();
        var argumentParts = new List<string>(
            profile.CliArgs.Count > 0
                ? profile.CliArgs.Skip(1)
                : Array.Empty<string>());
        argumentParts.AddRange(overrides.ExtraFlags);
        var extraArguments =
            argumentParts.Count > 0
                ? string.Join(" ", argumentParts)
                : null;
        var command =
            profile.CliArgs.Count > 0
            && !string.IsNullOrWhiteSpace(profile.CliArgs[0])
                ? profile.CliArgs[0]
                : null;
        var flags = new LaunchFlags(
            overrides.Yolo,
            null,
            extra,
            EffectiveSelection(overrides.Model, profile.Model),
            EffectiveSelection(overrides.Effort, profile.Effort),
            extraArguments,
            null,
            command);
        return JsonSerializer.Serialize(
            flags,
            LaunchFlagsJsonContext.Default.LaunchFlags);
    }

    private static void ApplyOverrides(
        List<string> arguments,
        LaunchProfile profile,
        LauncherOverrides overrides)
    {
        if (overrides.Yolo)
            arguments.Add("--dangerously-skip-permissions");
        var model = EffectiveSelection(overrides.Model, profile.Model);
        var effort = EffectiveSelection(overrides.Effort, profile.Effort);
        if (model is not null
            && profile.Adapter is "claude-code" or "codex" or "omp" or "pi")
        {
            arguments.Add("--model");
            arguments.Add(model);
        }
        if (effort is not null)
        {
            if (profile.Adapter == "claude-code")
            {
                arguments.Add("--effort");
                arguments.Add(effort);
            }
            else if (profile.Adapter == "codex")
            {
                arguments.Add("--config");
                arguments.Add($"model_reasoning_effort=\"{effort}\"");
            }
            else if (profile.Adapter is "omp" or "pi")
            {
                arguments.Add("--thinking");
                arguments.Add(effort);
            }
        }
        foreach (var flag in overrides.ExtraFlags)
            arguments.Add(flag);
        foreach (var environment in overrides.Env)
        {
            arguments.Add(
                $"--env={environment.Key}={environment.Value}");
        }
    }

    private static string? EffectiveSelection(
        string? overrideValue,
        string? profileValue)
    {
        var value = overrideValue ?? profileValue;
        return string.IsNullOrWhiteSpace(value)
            || string.Equals(
                value,
                "default",
                StringComparison.OrdinalIgnoreCase)
                ? null
                : value;
    }
}
