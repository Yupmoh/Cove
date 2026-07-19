using Cove.Protocol;

namespace Cove.Cli;

internal static class TaskRunCommands
{
    [CoveCommand("task launch-config get")]
        public static Task<int> TaskLaunchConfigGet(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.launch-config.get");

    [CoveCommand("task launch-config set")]
        public static Task<int> TaskLaunchConfigSet(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.launch-config.set");

    [CoveCommand("task binding get")]
        public static Task<int> TaskBindingGet(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.binding.get");

    [CoveCommand("task binding set")]
        public static Task<int> TaskBindingSet(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.binding.set");

    [CoveCommand("task binding resolve-profile")]
        public static Task<int> TaskBindingResolveProfile(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.binding.resolve-profile");

    [CoveCommand("task launch")]
        public static Task<int> TaskLaunch(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.launch");

    [CoveCommand("run list")]
        public static Task<int> RunList(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/run.list");

    [CoveCommand("run show")]
        public static Task<int> RunShow(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/run.show");

    [CoveCommand("run segments")]
        public static Task<int> RunSegments(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/run.segments");

    [CoveCommand("run cancel")]
        public static Task<int> RunCancel(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/run.cancel");

    [CoveCommand("run complete")]
        public static Task<int> RunComplete(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/run.complete");

    [CoveCommand("run resume")]
        public static Task<int> RunResume(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/run.resume");

    [CoveCommand("run set-pending-prompt")]
        public static Task<int> RunSetPendingPrompt(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/run.set-pending-prompt");

    [CoveCommand("task repeat set")]
        public static Task<int> TaskRepeatSet(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.repeat.set");

    [CoveCommand("task repeat get")]
        public static Task<int> TaskRepeatGet(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.repeat.get");

    [CoveCommand("task repeat pause")]
        public static Task<int> TaskRepeatPause(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.repeat.pause");

    [CoveCommand("task repeat resume")]
        public static Task<int> TaskRepeatResume(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.repeat.resume");

    [CoveCommand("task repeat skip-next")]
        public static Task<int> TaskRepeatSkipNext(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.repeat.skip-next");

    [CoveCommand("task repeat stop")]
        public static Task<int> TaskRepeatStop(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.repeat.stop");

    [CoveCommand("task repeat continue")]
        public static Task<int> TaskRepeatContinue(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.repeat.continue");

    [CoveCommand("task repeat finish")]
        public static Task<int> TaskRepeatFinish(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task.repeat.finish");

    [CoveCommand("task-board export")]
        public static Task<int> TaskBoardExport(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task-board.export");

    [CoveCommand("task-board diff")]
        public static Task<int> TaskBoardDiff(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/task-board.diff");
}
