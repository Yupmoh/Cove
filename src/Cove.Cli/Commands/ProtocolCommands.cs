using Cove.Protocol;

namespace Cove.Cli;

internal static class ProtocolCommands
{
    [CoveCommand("exec")]
    public static Task<int> Exec(CommandContext ctx)
    {
        var args = ctx.Args;
        if (args.Length < 1)
        {
            ctx.Stderr.WriteLine("usage: cove exec <dot.name> [--params '<json>']");
            return Task.FromResult(1);
        }
        string? paramsJson = null;
        for (var i = 1; i + 1 < args.Length; i++)
            if (args[i] == "--params")
                paramsJson = args[i + 1];
        var uri = "cove://commands/" + args[0];
        return paramsJson is null ? ctx.RouteCoreAsync(uri) : ctx.RouteCoreWithParamsAsync(uri, paramsJson);
    }

    [CoveCommand("protocol resolve")]
    public static Task<int> ProtocolResolve(CommandContext ctx)
    {
        var args = ctx.Args;
        if (args.Length < 1)
        {
            ctx.Stderr.WriteLine("usage: cove protocol resolve <uri>");
            return Task.FromResult(1);
        }
        var uri = args[0];
        return ctx.RouteCoreAsync($"cove://commands/protocol.resolve?uri={uri}");
    }
}
