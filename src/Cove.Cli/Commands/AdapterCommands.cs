using Cove.Protocol;

namespace Cove.Cli;

internal static class AdapterCommands
{
    [CoveCommand("adapter-env list")]
    public static Task<int> AdapterEnvList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/adapter-env.list");

    [CoveCommand("adapter-env save")]
    public static Task<int> AdapterEnvSave(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/adapter-env.save");

    [CoveCommand("adapter-env resolve")]
    public static Task<int> AdapterEnvResolve(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/adapter-env.resolve");

    [CoveCommand("extension list")]
    public static Task<int> ExtensionList(CommandContext ctx)
        => ctx.RouteCoreWithParamsAsync(
            "cove://commands/extension.list",
            null,
            data =>
            {
                if (data is not { } commands || commands.ValueKind != System.Text.Json.JsonValueKind.Array)
                {
                    ctx.Stderr.WriteLine("error: invalid_response");
                    return 1;
                }
                if (ctx.IsJson)
                {
                    ctx.Render(commands);
                }
                else
                {
                    foreach (var ext in commands.EnumerateArray()
                                 .OrderBy(e => e.GetProperty("adapter").GetString() ?? "", System.StringComparer.Ordinal)
                                 .ThenBy(e => e.GetProperty("method").GetString() ?? "", System.StringComparer.Ordinal))
                    {
                        ctx.Stdout.WriteLine(
                            $"{ext.GetProperty("command").GetString()}  (adapter: {ext.GetProperty("adapter").GetString()}, method: {ext.GetProperty("method").GetString()})");
                    }
                    ctx.Stdout.WriteLine($"Total: {commands.GetArrayLength()}");
                }
                return 0;
            },
            noAutostart: ctx.Args.Contains("--no-autostart"));

    [CoveCommand("extension run")]
    public static Task<int> ExtensionRun(CommandContext ctx)
    {
        var args = ctx.Args;
        if (args.Length < 1)
        {
            ctx.Stderr.WriteLine("usage: cove extension run <extension.adapter.method> [--params '<json>']");
            return Task.FromResult(1);
        }
        var command = args[0];
        string? paramsJson = null;
        for (var i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == "--params")
                paramsJson = args[i + 1];
        }
        using var payloadBuf = new System.IO.MemoryStream();
        using (var payloadWriter = new System.Text.Json.Utf8JsonWriter(payloadBuf))
        {
            payloadWriter.WriteStartObject();
            payloadWriter.WriteString("command", command);
            if (paramsJson is not null)
            {
                try
                {
                    using var paramsDoc = System.Text.Json.JsonDocument.Parse(paramsJson);
                    payloadWriter.WritePropertyName("params");
                    paramsDoc.RootElement.WriteTo(payloadWriter);
                }
                catch (System.Text.Json.JsonException)
                {
                    ctx.Stderr.WriteLine("error: invalid_params");
                    ctx.Stderr.WriteLine("usage: --params '<json>'");
                    return Task.FromResult(1);
                }
            }
            payloadWriter.WriteEndObject();
            payloadWriter.Flush();
        }
        return ctx.RouteCoreWithParamsAsync("cove://commands/extension.run", System.Text.Encoding.UTF8.GetString(payloadBuf.ToArray()));
    }
}
