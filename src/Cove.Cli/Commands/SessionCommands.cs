using Cove.Protocol;

namespace Cove.Cli;

internal static class SessionCommands
{
    [CoveCommand("session recent")]
    public static Task<int> SessionRecent(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/session.recent");
}
