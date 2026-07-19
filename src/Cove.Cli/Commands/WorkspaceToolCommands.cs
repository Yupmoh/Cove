using Cove.Protocol;

namespace Cove.Cli;

internal static class WorkspaceToolCommands
{
    [CoveCommand("editor open")]
        public static Task<int> EditorOpen(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/editor.open");

    [CoveCommand("editor save")]
        public static Task<int> EditorSave(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/editor.save");

    [CoveCommand("editor get-state")]
        public static Task<int> EditorGetState(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/editor.get-state");

    [CoveCommand("editor set-state")]
        public static Task<int> EditorSetState(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/editor.set-state");

    [CoveCommand("search query")]
        public static Task<int> SearchQuery(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/search.query");

    [CoveCommand("search get-state")]
        public static Task<int> SearchGetState(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/search.get-state");

    [CoveCommand("search set-state")]
        public static Task<int> SearchSetState(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/search.set-state");

    [CoveCommand("scm status")]
        public static Task<int> ScmStatus(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/scm.status");

    [CoveCommand("scm diff")]
        public static Task<int> ScmDiff(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/scm.diff");

    [CoveCommand("scm stage")]
        public static Task<int> ScmStage(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/scm.stage");

    [CoveCommand("scm commit")]
        public static Task<int> ScmCommit(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/scm.commit");

    [CoveCommand("scm blame")]
        public static Task<int> ScmBlame(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/scm.blame");

    [CoveCommand("viewer open")]
        public static Task<int> ViewerOpen(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/viewer.open");

    [CoveCommand("viewer get-state")]
        public static Task<int> ViewerGetState(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/viewer.get-state");
}
