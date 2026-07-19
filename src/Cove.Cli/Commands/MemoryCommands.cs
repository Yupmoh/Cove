using Cove.Protocol;

namespace Cove.Cli;

internal static class MemoryCommands
{
    [CoveCommand("memory add")]
        public static Task<int> MemoryAdd(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/memory.add");

    [CoveCommand("memory search")]
        public static Task<int> MemorySearch(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/memory.search");

    [CoveCommand("memory recall")]
        public static Task<int> MemoryRecall(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/memory.recall");

    [CoveCommand("memory show")]
        public static Task<int> MemoryShow(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/memory.show");

    [CoveCommand("memory supersede")]
        public static Task<int> MemorySupersede(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/memory.supersede");

    [CoveCommand("memory reindex")]
        public static Task<int> MemoryReindex(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/memory.reindex");

    [CoveCommand("memory consolidate")]
        public static Task<int> MemoryConsolidate(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/memory.consolidate");

    [CoveCommand("memory propose")]
        public static Task<int> MemoryPropose(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/memory.propose");

    [CoveCommand("memory proposal transition")]
        public static Task<int> MemoryProposalTransition(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/memory.proposal.transition");
}
