using Cove.Protocol;

namespace Cove.Cli;

internal static class KnowledgeCommands
{
    [CoveCommand("canvas action")]
        public static Task<int> CanvasAction(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/canvas.action");

    [CoveCommand("edits find")]
        public static Task<int> EditsFind(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/edits.find");

    [CoveCommand("vault search")]
        public static Task<int> VaultSearch(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/vault.search");

    [CoveCommand("vault resume")]
        public static Task<int> VaultResume(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/vault.resume");

    [CoveCommand("vault set-setting")]
        public static Task<int> VaultSetSetting(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/vault.set-setting");

    [CoveCommand("vault reindex")]
        public static Task<int> VaultReindex(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/vault.reindex");

    [CoveCommand("library list")]
        public static Task<int> LibraryList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/library.list");

    [CoveCommand("library materialize")]
        public static Task<int> LibraryMaterialize(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/library.materialize");

    [CoveCommand("timeline list")]
        public static Task<int> TimelineList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/timeline.list");

    [CoveCommand("timeline append")]
        public static Task<int> TimelineAppend(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/timeline.append");

    [CoveCommand("knowledge ping")]
        public static Task<int> KnowledgePing(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/knowledge.ping");

    [CoveCommand("blackboard post")]
        public static Task<int> BlackboardPost(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/blackboard.post");

    [CoveCommand("blackboard show")]
        public static Task<int> BlackboardShow(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/blackboard.show");
}
