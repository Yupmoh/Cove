using Cove.Protocol;

namespace Cove.Cli;

internal static class PerfBundleCommands
{
    [CoveCommand("perf bundle create")]
    public static Task<int> PerfBundleCreate(CommandContext ctx)
    {
        var args = ctx.Args;
        var tracePath = ArgValue(args, "--trace") ?? "";
        return ctx.RouteCoreWithParamsAsync("cove://commands/perf.bundle.create", BuildStringParams("tracePath", tracePath));
    }

    [CoveCommand("perf bundle list")]
    public static Task<int> PerfBundleList(CommandContext ctx)
            => ctx.RouteCoreWithParamsAsync("cove://commands/perf.bundle.list", null);

    [CoveCommand("perf bundle delete")]
    public static Task<int> PerfBundleDelete(CommandContext ctx)
    {
        var args = ctx.Args;
        var bundlePath = ArgValue(args, "--path") ?? (args.Length > 0 ? args[0] : "");
        return ctx.RouteCoreWithParamsAsync("cove://commands/perf.bundle.delete", BuildStringParams("bundlePath", bundlePath));
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
}
