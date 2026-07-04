using System.Threading.Tasks;
using Cove.Platform;
using Cove.Protocol;

namespace Cove.Cli;

internal static class CliCommands
{
    [CoveCommand("version")]
    public static Task<int> Version(CommandContext ctx)
    {
        ctx.Stdout.WriteLine(CoveBuild.InformationalVersion);
        return Task.FromResult(0);
    }

    [CoveCommand("pane list")]
    public static Task<int> PaneList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/pane.list");
}
