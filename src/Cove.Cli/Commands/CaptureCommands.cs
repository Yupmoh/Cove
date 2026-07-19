using Cove.Protocol;

namespace Cove.Cli;

internal static class CaptureCommands
{
    [CoveCommand("capture start")]
    public static Task<int> CaptureStart(CommandContext ctx)
    {
        var args = ctx.Args;
        var region = ArgValue(args, "--region") ?? "fullscreen";
        var bayId = ArgValue(args, "--bay") ?? "";
        var audio = args.Contains("--audio");
        var mic = args.Contains("--mic");
        var cursor = args.Contains("--cursor");
        return ctx.RouteCoreWithParamsAsync("cove://commands/capture.start", BuildCaptureStartParams(bayId, region, audio, mic, cursor));
    }

    [CoveCommand("capture stop")]
    public static Task<int> CaptureStop(CommandContext ctx)
    {
        var args = ctx.Args;
        var id = ArgValue(args, "--id") ?? (args.Length > 0 ? args[0] : "");
        return ctx.RouteCoreWithParamsAsync("cove://commands/capture.stop", BuildStringParams("id", id));
    }

    [CoveCommand("capture list")]
    public static Task<int> CaptureList(CommandContext ctx)
            => ctx.RouteCoreWithParamsAsync("cove://commands/capture.list", null);

    [CoveCommand("capture delete")]
    public static Task<int> CaptureDelete(CommandContext ctx)
    {
        var args = ctx.Args;
        var id = ArgValue(args, "--id") ?? (args.Length > 0 ? args[0] : "");
        return ctx.RouteCoreWithParamsAsync("cove://commands/capture.delete", BuildStringParams("id", id));
    }

    [CoveCommand("capture flag")]
    public static Task<int> CaptureFlag(CommandContext ctx)
    {
        var args = ctx.Args;
        var id = ArgValue(args, "--id") ?? (args.Length > 0 ? args[0] : "");
        var label = ArgValue(args, "--label") ?? "";
        return ctx.RouteCoreWithParamsAsync("cove://commands/capture.flag", BuildStringPairParams("id", id, "label", label));
    }

    [CoveCommand("capture show")]
    public static Task<int> CaptureShow(CommandContext ctx)
    {
        var args = ctx.Args;
        var id = ArgValue(args, "--id") ?? (args.Length > 0 ? args[0] : "");
        return ctx.RouteCoreWithParamsAsync("cove://commands/capture.show", BuildStringParams("id", id));
    }

    [CoveCommand("capture attach")]
    public static Task<int> CaptureAttach(CommandContext ctx)
    {
        var args = ctx.Args;
        var captureId = ArgValue(args, "--capture") ?? "";
        var taskId = ArgValue(args, "--task") ?? "";
        return ctx.RouteCoreWithParamsAsync("cove://commands/capture.attach", BuildStringPairParams("captureId", captureId, "taskId", taskId));
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

    private static string BuildStringPairParams(
            string firstPropertyName,
            string firstValue,
            string secondPropertyName,
            string secondValue)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString(firstPropertyName, firstValue);
            writer.WriteString(secondPropertyName, secondValue);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string BuildCaptureStartParams(string bayId, string region, bool audio, bool mic, bool cursor)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("bayId", bayId);
            writer.WriteString("region", region);
            writer.WriteBoolean("audio", audio);
            writer.WriteBoolean("mic", mic);
            writer.WriteBoolean("cursor", cursor);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
