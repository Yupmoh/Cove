using System.Text.Json;
using Cove.Protocol;
using Cove.Tui.Attach;

namespace Cove.Cli;

internal static class NookCommands
{
    [CoveCommand("nook list")]
        public static Task<int> NookList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/nook.list");

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
