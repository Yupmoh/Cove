using System.Text.Json;
using Cove.Protocol;

namespace Cove.Cli;

internal static class WorkspaceCommands
{
    [CoveCommand("workspace context")]
    public static Task<int> Context(CommandContext ctx)
    {
        var nookId = ArgValue(ctx.Args, "--nook-id");
        var parameters = nookId is null
            ? null
            : JsonSerializer.Serialize(
                new WorkspaceContextParams(nookId),
                CoveJsonContext.Default.WorkspaceContextParams);
        return ctx.RouteCoreWithParamsAsync(
            "cove://commands/workspace.context",
            parameters);
    }

    private static string? ArgValue(string[] args, string flag)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : null;
    }
}
