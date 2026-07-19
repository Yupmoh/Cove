using Cove.Protocol;

namespace Cove.Cli;

internal static class ReviewCommands
{
    [CoveCommand("review add-comment")]
        public static Task<int> ReviewAddComment(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/review.add-comment");

    [CoveCommand("review list-comments")]
        public static Task<int> ReviewListComments(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/review.list-comments");

    [CoveCommand("review resolve")]
        public static Task<int> ReviewResolve(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/review.resolve");

    [CoveCommand("review reopen")]
        public static Task<int> ReviewReopen(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/review.reopen");

    [CoveCommand("review close")]
        public static Task<int> ReviewClose(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/review.close");

    [CoveCommand("review re-anchor")]
        public static Task<int> ReviewReAnchor(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/review.re-anchor");

    [CoveCommand("review audit")]
        public static Task<int> ReviewAudit(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/review.audit");

    [CoveCommand("review telemetry")]
        public static Task<int> ReviewTelemetry(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/review.telemetry");

    [CoveCommand("attribution record")]
        public static Task<int> AttributionRecord(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/attribution.record");

    [CoveCommand("attribution find-by-line")]
        public static Task<int> AttributionFindByLine(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/attribution.find-by-line");

    [CoveCommand("attribution find-by-range")]
        public static Task<int> AttributionFindByRange(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/attribution.find-by-range");

    [CoveCommand("attribution find-by-tool-use")]
        public static Task<int> AttributionFindByToolUse(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/attribution.find-by-tool-use");

    [CoveCommand("review dispatch")]
        public static Task<int> ReviewDispatch(CommandContext ctx)
            => ctx.RouteCoreAsync("cove://commands/review.dispatch");
}
