using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Activity;

public static class ActivityCommands
{
    [CoveCommand("cove://commands/activity.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.Activity is not { } aggregate)
            return Task.FromResult(ctx.Fail("not_ready", "activity aggregate not available"));

        var cards = aggregate.List().Select(c => new ActivityCardDto(
            c.NookId,
            c.Adapter,
            c.Name,
            c.Bay,
            c.Shore,
            c.Status.ToString().ToLowerInvariant(),
            c.StopReason,
            c.ActiveSubagents,
            c.LastEvent,
            c.LastEventAt)).ToList();

        return Task.FromResult(ctx.Ok(new ActivityListResult(cards), CoveJsonContext.Default.ActivityListResult));
    }

    [CoveCommand("cove://commands/activity.needs-input")]
    public static Task<ControlResponse> NeedsInput(EngineDispatchContext ctx)
    {
        if (ctx.Activity is not { } aggregate)
            return Task.FromResult(ctx.Fail("not_ready", "activity aggregate not available"));

        var cards = aggregate.NeedsInputCards().Select(c => new ActivityCardDto(
            c.NookId,
            c.Adapter,
            c.Name,
            c.Bay,
            c.Shore,
            c.Status.ToString().ToLowerInvariant(),
            c.StopReason,
            c.ActiveSubagents,
            c.LastEvent,
            c.LastEventAt)).ToList();

        return Task.FromResult(ctx.Ok(new ActivityListResult(cards), CoveJsonContext.Default.ActivityListResult));
    }
}
