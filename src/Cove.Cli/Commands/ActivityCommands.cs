using Cove.Protocol;

namespace Cove.Cli;

internal static class ActivityCommands
{
    [CoveCommand("activity list")]
        public static Task<int> ActivityList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/activity.list");

    [CoveCommand("activity acknowledge")]
        public static Task<int> ActivityAcknowledge(CommandContext ctx)
            => ctx.RouteCoreWithParamsAsync("cove://commands/activity.acknowledge", BuildNookIdParams(ctx.Args));

    [CoveCommand("hook emit")]
    public static async Task<int> HookEmit(CommandContext ctx)
    {
        var args = ctx.Args;
        var adapter = ArgValue(args, "--adapter");
        var nookId = ArgValue(args, "--nook-id");
        var verbose = Array.IndexOf(args, "--verbose") >= 0;
        var @event = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
            ? args[0]
            : null;
        if (string.IsNullOrWhiteSpace(adapter) || string.IsNullOrWhiteSpace(@event))
        {
            await ctx.Stderr.WriteLineAsync("error: event and --adapter required");
            return 1;
        }

        System.Text.Json.JsonElement payload;
        if (ctx.IsInputRedirected)
        {
            var stdinPayload = await ctx.Stdin.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(stdinPayload))
            {
                using var emptyDocument = System.Text.Json.JsonDocument.Parse("{}");
                payload = emptyDocument.RootElement.Clone();
            }
            else
            {
                try
                {
                    using var payloadDocument = System.Text.Json.JsonDocument.Parse(stdinPayload);
                    payload = payloadDocument.RootElement.Clone();
                }
                catch (System.Text.Json.JsonException)
                {
                    await ctx.Stderr.WriteLineAsync("error: invalid_params");
                    return 1;
                }
            }
        }
        else
        {
            using var emptyDocument = System.Text.Json.JsonDocument.Parse("{}");
            payload = emptyDocument.RootElement.Clone();
        }

        var parameters = new HookEmitParams(adapter, @event, nookId, payload);
        var paramsJson = System.Text.Json.JsonSerializer.Serialize(
            parameters,
            CoveJsonContext.Default.HookEmitParams);
        return await ctx.RouteCoreWithParamsAsync(
            "cove://commands/hook.emit",
            paramsJson,
            data =>
            {
                if (verbose)
                    ctx.Stderr.WriteLine(data?.GetRawText() ?? "{}");
                return 0;
            });
    }

    [CoveCommand("hook context")]
    public static async Task<int> HookContext(CommandContext ctx)
    {
        var adapter = ArgValue(ctx.Args, "--adapter");
        if (!string.Equals(adapter, "cursor-agent", StringComparison.Ordinal))
        {
            await ctx.Stderr.WriteLineAsync("error: --adapter cursor-agent required");
            return 1;
        }
        var skillPath = Environment.GetEnvironmentVariable("COVE_SKILL_PATH");
        if (string.IsNullOrWhiteSpace(skillPath))
        {
            await ctx.Stdout.WriteLineAsync("{}");
            return 0;
        }
        if (!ctx.Files.FileExists(skillPath))
        {
            await ctx.Stderr.WriteLineAsync("error: COVE_SKILL_PATH does not exist");
            await ctx.Stdout.WriteLineAsync("{}");
            return 1;
        }
        var content = await ctx.Files.ReadAllTextAsync(skillPath, CancellationToken.None);
        using var buffer = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("additional_context", content);
            writer.WriteEndObject();
            writer.Flush();
        }
        await ctx.Stdout.WriteLineAsync(
            System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length)));
        return 0;
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

    private static string? BuildNookIdParams(string[] args)
    {
        var nookId = ArgValue(args, "--nook-id");
        if (nookId is null) return null;
        return "{\"nookId\":\"" + EscapeJson(nookId) + "\"}";
    }

    private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
