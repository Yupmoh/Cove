using Cove.Protocol;

namespace Cove.Cli;

internal static class TaskMetadataCommands
{
    [CoveCommand("task status list")]
        public static Task<int> TaskStatusList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.status.list");

    [CoveCommand("task status create")]
        public static Task<int> TaskStatusCreate(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.status.create");

    [CoveCommand("task status delete")]
        public static Task<int> TaskStatusDelete(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.status.delete");

    [CoveCommand("task status reorder")]
        public static Task<int> TaskStatusReorder(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.status.reorder");

    [CoveCommand("task status hide")]
        public static Task<int> TaskStatusHide(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.status.set-hidden");

    [CoveCommand("task label list")]
        public static Task<int> TaskLabelList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.label.list");

    [CoveCommand("task label create")]
        public static Task<int> TaskLabelCreate(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.label.create");

    [CoveCommand("task label delete")]
        public static Task<int> TaskLabelDelete(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.label.delete");

    [CoveCommand("task label assign")]
        public static Task<int> TaskLabelAssign(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.label.assign");

    [CoveCommand("task label unassign")]
        public static Task<int> TaskLabelUnassign(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.label.unassign");

    [CoveCommand("task label reorder")]
        public static Task<int> TaskLabelReorder(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.label.reorder");

    [CoveCommand("task label filter")]
        public static Task<int> TaskLabelFilter(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.label.filter");

    [CoveCommand("task comment add")]
        public static Task<int> TaskCommentAdd(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.comment.add");

    [CoveCommand("task comment list")]
        public static Task<int> TaskCommentList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.comment.list");

    [CoveCommand("task comment count")]
        public static Task<int> TaskCommentCount(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.comment.count");

    [CoveCommand("task comment delete")]
        public static Task<int> TaskCommentDelete(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.comment.delete");
}
