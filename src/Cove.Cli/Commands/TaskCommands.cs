using Cove.Protocol;

namespace Cove.Cli;

internal static class TaskCommands
{
    [CoveCommand("task list")]
        public static Task<int> TaskList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.list");

    [CoveCommand("task get")]
        public static Task<int> TaskGet(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.get");

    [CoveCommand("task create")]
        public static Task<int> TaskCreate(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.create");

    [CoveCommand("task update")]
        public static Task<int> TaskUpdate(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.update");

    [CoveCommand("task delete")]
        public static Task<int> TaskDelete(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.delete");

    [CoveCommand("task set-in-review")]
        public static Task<int> TaskSetInReview(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.set-in-review");

    [CoveCommand("task set-done")]
        public static Task<int> TaskSetDone(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.set-done");

    [CoveCommand("task claim")]
        public static Task<int> TaskClaim(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.claim");

    [CoveCommand("task run-now")]
        public static Task<int> TaskRunNow(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.run-now");
}
