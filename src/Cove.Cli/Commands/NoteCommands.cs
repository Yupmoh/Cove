using Cove.Protocol;

namespace Cove.Cli;

internal static class NoteCommands
{
    [CoveCommand("note list")]
        public static Task<int> NoteList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/note.list");

    [CoveCommand("note get")]
        public static Task<int> NoteGet(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/note.get");

    [CoveCommand("note create")]
        public static Task<int> NoteCreate(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/note.create");

    [CoveCommand("note update")]
        public static Task<int> NoteUpdate(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/note.update");

    [CoveCommand("note delete")]
        public static Task<int> NoteDelete(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/note.delete");

    [CoveCommand("note search")]
        public static Task<int> NoteSearch(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/note.search");

    [CoveCommand("note read")]
        public static Task<int> NoteRead(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/note.read");

    [CoveCommand("note write")]
        public static Task<int> NoteWrite(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/note.write");

    [CoveCommand("note history")]
        public static Task<int> NoteHistory(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/note.history");

    [CoveCommand("note media save")]
        public static Task<int> NoteMediaSave(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/note.media.save");

    [CoveCommand("note get-state")]
        public static Task<int> NoteGetState(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/note.get-state");

    [CoveCommand("note save-state")]
        public static Task<int> NoteSaveState(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/note.save-state");
}
