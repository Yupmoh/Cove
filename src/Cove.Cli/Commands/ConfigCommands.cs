using Cove.Protocol;

namespace Cove.Cli;

internal static class ConfigCommands
{
    [CoveCommand("config get")]
        public static Task<int> ConfigGet(CommandContext ctx)
        {
            var args = ctx.Args;
            if (args.Length < 1)
            {
                ctx.Stderr.WriteLine("usage: cove config get <key>");
                return Task.FromResult(1);
            }
            var key = args[0];
            var paramsJson = System.Text.Json.JsonSerializer.Serialize(
                new ConfigGetParams(key),
                CoveJsonContext.Default.ConfigGetParams);
            return ctx.RouteCoreWithParamsAsync(
                "cove://commands/config.get",
                paramsJson,
                data =>
                {
                    if (data is not { } result ||
                        result.ValueKind != System.Text.Json.JsonValueKind.Object ||
                        !result.TryGetProperty("value", out var value))
                    {
                        ctx.Stderr.WriteLine("error: invalid_response");
                        return 1;
                    }
                    if (ctx.IsJson)
                        ctx.Render(result);
                    else
                        ctx.Stdout.WriteLine(value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() : value.GetRawText());
                    return 0;
                },
                successfulErrorCode: "not_found",
                noAutostart: args.Contains("--no-autostart"));
        }

    [CoveCommand("config set")]
        public static Task<int> ConfigSet(CommandContext ctx)
        {
            var args = ctx.Args;
            if (args.Length < 2)
            {
                ctx.Stderr.WriteLine("usage: cove config set <key> <value>");
                return Task.FromResult(1);
            }
            var key = args[0];
            var value = args[1];
            var paramsJson = System.Text.Json.JsonSerializer.Serialize(
                new ConfigSetParams(key, value),
                CoveJsonContext.Default.ConfigSetParams);
            return ctx.RouteCoreWithParamsAsync(
                "cove://commands/config.set",
                paramsJson,
                data =>
                {
                    if (ctx.IsJson)
                    {
                        if (data is { } result)
                            ctx.Render(result);
                        else
                            ctx.Stdout.WriteLine("{}");
                    }
                    else
                    {
                        ctx.Stdout.WriteLine($"set {key} = {value}");
                    }
                    return 0;
                },
                noAutostart: args.Contains("--no-autostart"));
        }
}
