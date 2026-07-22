using System.Text.Json;
using Cove.Protocol;
using Cove.Tui.Attach;

namespace Cove.Cli;

internal static class NookCommands
{
    [CoveCommand("nook list")]
        public static Task<int> NookList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/nook.list");

    [CoveCommand("nook open")]
    public static Task<int> NookOpen(CommandContext ctx)
    {
        var nookType = FirstPositional(ctx.Args);
        if (nookType is null)
        {
            ctx.Stderr.WriteLine("usage: cove nook open terminal");
            return Task.FromResult(1);
        }
        var parameters = new NookOpenParams(
            nookType,
            ArgValue(ctx.Args, "--command"),
            ArgValues(ctx.Args, "--arg"),
            ArgValue(ctx.Args, "--cwd"),
            ArgValue(ctx.Args, "--relative-to"),
            ArgValue(ctx.Args, "--placement") ?? "right",
            ArgValue(ctx.Args, "--bay-id"),
            IntValue(ctx.Args, "--cols") ?? 80,
            IntValue(ctx.Args, "--rows") ?? 24,
            ArgValue(ctx.Args, "--url"));
        var json = JsonSerializer.Serialize(
            parameters,
            CoveJsonContext.Default.NookOpenParams);
        return ctx.RouteCoreWithParamsAsync(
            "cove://commands/nook.open",
            json);
    }

    [CoveCommand("nook open-many")]
    public static Task<int> NookOpenMany(CommandContext ctx)
    {
        var itemValues = ArgValues(ctx.Args, "--item");
        var relativeTo = ArgValue(ctx.Args, "--relative-to");
        var placement = ArgValue(ctx.Args, "--placement");
        var balance = ArgValue(ctx.Args, "--balance");
        if (itemValues.Length == 0
            || string.IsNullOrWhiteSpace(relativeTo)
            || placement is not ("left" or "right" or "above" or "below" or "new-shore")
            || balance is not null and not ("left" or "right" or "above" or "below"))
        {
            ctx.Stderr.WriteLine(
                "usage: cove nook open-many --item <json> [--item <json> ...] --relative-to <nook-id> --placement <placement> [--balance <placement>]");
            return Task.FromResult(1);
        }
        var items = new NookOpenManyItem[itemValues.Length];
        try
        {
            for (var index = 0; index < itemValues.Length; index++)
            {
                items[index] = JsonSerializer.Deserialize(
                    itemValues[index],
                    CoveJsonContext.Default.NookOpenManyItem)
                    ?? throw new JsonException("nook item is required");
            }
        }
        catch (JsonException)
        {
            ctx.Stderr.WriteLine(
                "usage: cove nook open-many --item <json> [--item <json> ...] --relative-to <nook-id> --placement <placement> [--balance <placement>]");
            return Task.FromResult(1);
        }
        var json = JsonSerializer.Serialize(
            new NookOpenManyParams(items, relativeTo, placement, balance),
            CoveJsonContext.Default.NookOpenManyParams);
        return ctx.RouteCoreWithParamsAsync(
            "cove://commands/nook.open-many",
            json);
    }

    [CoveCommand("nook close")]
    public static Task<int> NookClose(CommandContext ctx)
    {
        var nookId = FirstPositional(ctx.Args);
        if (nookId is null)
        {
            ctx.Stderr.WriteLine("usage: cove nook close <nook-id>");
            return Task.FromResult(1);
        }
        var json = JsonSerializer.Serialize(
            new NookRefParams(nookId),
            CoveJsonContext.Default.NookRefParams);
        return ctx.RouteCoreWithParamsAsync(
            "cove://commands/nook.close",
            json);
    }

    [CoveCommand("nook close-others")]
    public static Task<int> NookCloseOthers(CommandContext ctx)
    {
        var nookId = FirstPositional(ctx.Args);
        var scope = ArgValue(ctx.Args, "--scope") ?? "same-shore";
        if (nookId is null
            || scope is not ("same-shore" or "same-bay"))
        {
            ctx.Stderr.WriteLine(
                "usage: cove nook close-others <keep-nook-id> [--scope <same-shore|same-bay>]");
            return Task.FromResult(1);
        }
        var json = JsonSerializer.Serialize(
            new NookCloseOthersParams(nookId, scope),
            CoveJsonContext.Default.NookCloseOthersParams);
        return ctx.RouteCoreWithParamsAsync(
            "cove://commands/nook.close-others",
            json);
    }

    [CoveCommand("nook stack")]
    public static Task<int> NookStack(CommandContext ctx)
    {
        var nookId = FirstPositional(ctx.Args);
        var placement = ArgValue(ctx.Args, "--placement");
        if (nookId is null
            || placement is not ("left" or "right" or "above" or "below"))
        {
            ctx.Stderr.WriteLine(
                "usage: cove nook stack <nook-id> --placement <left|right|above|below>");
            return Task.FromResult(1);
        }
        var json = JsonSerializer.Serialize(
            new NookStackParams(nookId, placement),
            CoveJsonContext.Default.NookStackParams);
        return ctx.RouteCoreWithParamsAsync(
            "cove://commands/nook.stack",
            json);
    }

    [CoveCommand("nook restart")]
    public static Task<int> NookRestart(CommandContext ctx)
    {
        var nookId = FirstPositional(ctx.Args);
        if (nookId is null)
        {
            ctx.Stderr.WriteLine(
                "usage: cove nook restart <nook-id>");
            return Task.FromResult(1);
        }
        var parameters = new NookRestartParams(
            nookId,
            ArgValue(ctx.Args, "--mode") ?? "fresh",
            !ctx.Args.Contains("--no-preserve-scrollback"),
            ArgValue(ctx.Args, "--command"),
            ArgValues(ctx.Args, "--arg"),
            ArgValue(ctx.Args, "--cwd"),
            ArgValue(ctx.Args, "--resume-fallback") ?? "none");
        var json = JsonSerializer.Serialize(
            parameters,
            CoveJsonContext.Default.NookRestartParams);
        return ctx.RouteCoreWithParamsAsync(
            "cove://commands/nook.restart",
            json,
            data => RenderRestart(ctx, data));
    }

    [CoveCommand("attach")]
        public static async Task<int> Attach(CommandContext ctx)
        {
            var args = ctx.Args;
            var raw = args.Length > 0 && args[0] == "--raw";
            if (raw)
            {
                if (args.Length < 2)
                {
                    ctx.Stderr.WriteLine("usage: cove attach --raw <session>");
                    return 1;
                }
                var session = args[1];
                using var attachBuf = new System.IO.MemoryStream();
                using (var attachWriter = new System.Text.Json.Utf8JsonWriter(attachBuf))
                {
                    attachWriter.WriteStartObject();
                    attachWriter.WriteString("session", session);
                    attachWriter.WriteEndObject();
                    attachWriter.Flush();
                }
                return await ctx.RouteCoreWithParamsAsync("cove://commands/attach.raw", System.Text.Encoding.UTF8.GetString(attachBuf.ToArray()));
            }
            var nookId = args.Length > 0 ? args[0] : "";
            return await AttachCompositor.RunAsync(ctx.Paths, ctx.Endpoint, nookId, ctx.Source);
        }

    [CoveCommand("nook-types list")]
        public static Task<int> NookTypesList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/nook-types.list");

    private static int RenderRestart(
        CommandContext ctx,
        JsonElement? data)
    {
        if (data is not { } element)
        {
            ctx.Stderr.WriteLine("error: invalid_response");
            return 1;
        }
        if (ctx.IsJson)
        {
            ctx.Render(element);
            return 0;
        }
        var result = element.Deserialize(
            CoveJsonContext.Default.NookRestartResult);
        if (result is null)
        {
            ctx.Stderr.WriteLine("error: invalid_response");
            return 1;
        }
        ctx.Stdout.WriteLine(
            $"restarted {result.NookId} ({result.Outcome})");
        return 0;
    }

    private static string? FirstPositional(string[] args)
    {
        var valueFlags = new HashSet<string>(
        [
            "--mode",
            "--command",
            "--arg",
            "--cwd",
            "--resume-fallback",
            "--relative-to",
            "--placement",
            "--bay-id",
            "--cols",
            "--rows",
            "--url",
            "--item",
            "--balance",
            "--scope",
        ]);
        for (var index = 0; index < args.Length; index++)
        {
            if (valueFlags.Contains(args[index]))
            {
                index++;
                continue;
            }
            if (!args[index].StartsWith(
                    "--",
                    StringComparison.Ordinal))
            {
                return args[index];
            }
        }
        return null;
    }

    private static string? ArgValue(
        string[] args,
        string flag)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : null;
    }

    private static int? IntValue(
        string[] args,
        string flag) =>
        int.TryParse(
            ArgValue(args, flag),
            out var value)
            ? value
            : null;

    private static string[] ArgValues(
        string[] args,
        string flag)
    {
        var values = new List<string>();
        for (var index = 0; index + 1 < args.Length; index++)
        {
            if (args[index] != flag)
                continue;
            values.Add(args[++index]);
        }
        return values.ToArray();
    }
}
