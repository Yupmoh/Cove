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

    [CoveCommand("skills list")]
    public static Task<int> SkillsList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/skills.index");

    [CoveCommand("skills resolve-prompt-sigils")]
    public static Task<int> SkillsResolvePromptSigils(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/skills.resolve-prompt-sigils");

    [CoveCommand("agent definition list")]
    public static Task<int> AgentDefinitionList(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/agent.definition.list");

    [CoveCommand("agent definition show")]
    public static Task<int> AgentDefinitionShow(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/agent.definition.show");

    [CoveCommand("agent definition delete")]
    public static Task<int> AgentDefinitionDelete(CommandContext ctx)
        => ctx.RouteCoreAsync("cove://commands/agent.definition.delete");
}
