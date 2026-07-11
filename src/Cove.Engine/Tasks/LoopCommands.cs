using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Tasks;

public static class LoopCommands
{
    [CoveCommand("cove://commands/task.run-now")]
    public static async Task<ControlResponse> RunNow(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.RunNowParams) is not { } p)
            return ctx.Fail("invalid_params", "run-now params required (cardId)");
        var card = svc.GetCard(p.CardId);
        if (card is null)
            return ctx.Fail("not_found", "card not found");
        var run = await svc.CreateRunAsync(p.CardId, card.BayId, card.LaunchConfigJson, backgrounded: true);
        return ctx.Ok(new RunNowResult(true, run?.Id, null), CoveJsonContext.Default.RunNowResult);
    }

    [CoveCommand("cove://commands/task.repeat.continue")]
    public static async Task<ControlResponse> Continue(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.RepeatContinueParams) is not { } p)
            return ctx.Fail("invalid_params", "repeat continue params required (cardId)");
        var schedule = svc.GetSchedule(p.CardId);
        if (schedule is null)
            return ctx.Fail("not_found", "no schedule for card");
        await svc.UpdateScheduleAsync(p.CardId, paused: null, skipNext: null, nextFireAt: null, lastFiredAt: null, pendingIntent: "continue");
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/task.repeat.finish")]
    public static async Task<ControlResponse> Finish(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.RepeatFinishParams) is not { } p)
            return ctx.Fail("invalid_params", "repeat finish params required (cardId)");
        var schedule = svc.GetSchedule(p.CardId);
        if (schedule is null)
            return ctx.Fail("not_found", "no schedule for card");
        await svc.UpdateScheduleAsync(p.CardId, paused: null, skipNext: null, nextFireAt: null, lastFiredAt: null, pendingIntent: "finish");
        return ctx.Ok();
    }
}
