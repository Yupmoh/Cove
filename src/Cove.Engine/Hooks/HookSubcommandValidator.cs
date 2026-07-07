using System.Text.Json;

namespace Cove.Engine.Hooks;

public sealed record HookSubcommandResult(bool Valid, int ExitCode, string? Status, string? Subcommand = null, string? Reason = null, bool ActivityHooks = false);

public static class HookSubcommandValidator
{
    public static HookSubcommandResult ValidateOutput(JsonElement output)
    {
        if (output.ValueKind != JsonValueKind.Object)
            return new HookSubcommandResult(false, 2, null, Reason: "output must be a JSON object");

        if (!output.TryGetProperty("subcommand", out var subEl) || subEl.ValueKind != JsonValueKind.String)
            return new HookSubcommandResult(false, 2, null, Reason: "missing or invalid 'subcommand' field");

        var subcommand = subEl.GetString()!;
        if (subcommand is not "install" and not "uninstall" and not "status")
            return new HookSubcommandResult(false, 2, null, subcommand, Reason: $"unknown subcommand: {subcommand}");

        if (!output.TryGetProperty("installed", out var statusEl) || statusEl.ValueKind != JsonValueKind.String)
            return new HookSubcommandResult(false, 2, null, subcommand, Reason: "missing or invalid 'installed' field");

        var status = statusEl.GetString()!;
        if (status is not "installed" and not "uninstalled")
            return new HookSubcommandResult(false, 2, null, subcommand, Reason: $"invalid status: {status}");

        var activityHooks = false;
        if (output.TryGetProperty("activityHooks", out var ahEl) && ahEl.ValueKind == JsonValueKind.False)
            activityHooks = false;
        else if (ahEl.ValueKind == JsonValueKind.True)
            activityHooks = true;

        string? reason = null;
        if (output.TryGetProperty("reason", out var reasonEl) && reasonEl.ValueKind == JsonValueKind.String)
            reason = reasonEl.GetString();

        return new HookSubcommandResult(true, 0, status, subcommand, reason, activityHooks);
    }

    public static HookSubcommandResult ValidateUnknown(string subcommand)
    {
        return new HookSubcommandResult(false, 2, null, subcommand, Reason: $"unknown subcommand: {subcommand}");
    }
}
