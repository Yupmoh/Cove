using Cove.Protocol;

namespace Cove.Cli;

internal static class DiagnosticsCommands
{
    [CoveCommand("diagnostics status")]
    public static Task<int> DiagnosticsStatus(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/diagnostics.status");

    [CoveCommand("diagnostics snapshot")]
    public static Task<int> DiagnosticsSnapshot(CommandContext ctx)
    {
        var args = ctx.Args;
        if (!TryParseIntArg(args, "--nooks", out var activeNooks) ||
            !TryParseIntArg(args, "--bays", out var activeBays) ||
            !TryParseIntArg(args, "--agents", out var activeAgents))
        {
            ctx.Stderr.WriteLine("error: invalid_params");
            ctx.Stderr.WriteLine("usage: cove diagnostics snapshot [--nooks <number>] [--bays <number>] [--agents <number>]");
            return Task.FromResult(1);
        }
        return ctx.RouteCoreWithParamsAsync(
            "cove://commands/diagnostics.snapshot.take",
            BuildDiagnosticsSnapshotParams(activeNooks, activeBays, activeAgents));
    }

    [CoveCommand("diagnostics snapshots")]
    public static Task<int> DiagnosticsSnapshots(CommandContext ctx)
            => ctx.RouteCoreWithParamsAsync("cove://commands/diagnostics.snapshot.list", null);

    [CoveCommand("diagnostics export")]
    public static Task<int> DiagnosticsExport(CommandContext ctx)
    {
        var args = ctx.Args;
        var path = ArgValue(args, "--path") ?? (args.Length > 0 ? args[0] : "");
        return ctx.RouteCoreWithParamsAsync("cove://commands/diagnostics.export", BuildStringParams("path", path));
    }

    private static string? ArgValue(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag)
                return args[i + 1];
        }
        return null;
    }

    private static bool TryParseIntArg(string[] args, string flag, out int value)
            => int.TryParse(
                ArgValue(args, flag) ?? "0",
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);

    private static string BuildStringParams(string propertyName, string value)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString(propertyName, value);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string BuildDiagnosticsSnapshotParams(int activeNooks, int activeBays, int activeAgents)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("activeNooks", activeNooks);
            writer.WriteNumber("activeBays", activeBays);
            writer.WriteNumber("activeAgents", activeAgents);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
