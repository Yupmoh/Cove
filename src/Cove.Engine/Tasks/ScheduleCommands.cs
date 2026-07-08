using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;
using Cove.Tasks.Schedules;

namespace Cove.Engine.Tasks;

public static class ScheduleCommands
{
    private static readonly Cove.Tasks.Schedules.ICronExpander CronExpander = new Cove.Tasks.Schedules.HandRolledCronExpander();

    [CoveCommand("cove://commands/task.repeat.set")]
    public static async Task<ControlResponse> Set(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ScheduleSetRouteParams) is not { } p)
            return ctx.Fail("invalid_params", "repeat set params required");
        var card = svc.GetCard(p.CardId);
        if (card is null)
            return ctx.Fail("not_found", "card not found");
        var knownStatuses = svc.ListStatuses(card.WorkspaceId, includeHidden: true).Select(s => s.Id).ToHashSet();
        var setParams = new Cove.Tasks.Schedules.ScheduleSetParams(p.CardId, p.TriggerKind, p.Cron, p.Tz, p.At, p.CompletionRule, p.MarkDoneBy, p.BlockOverlap, p.HomeStatusId);
        var result = Cove.Tasks.Schedules.ScheduleValidator.Validate(setParams, knownStatuses, CronExpander);
        if (!result.IsValid)
            return ctx.Ok(new ScheduleValidationResultDto(false, result.Errors.ToArray(), null), CoveJsonContext.Default.ScheduleValidationResultDto);
        var row = new Cove.Tasks.Schedules.ScheduleRow
        {
            CardId = p.CardId,
            TriggerKind = p.TriggerKind,
            Cron = p.Cron,
            Tz = p.Tz,
            At = p.At,
            CompletionRule = p.CompletionRule ?? "loop",
            MarkDoneBy = p.MarkDoneBy ?? "agent",
            BlockOverlap = p.BlockOverlap ?? true,
            HomeStatusId = p.HomeStatusId,
            Paused = false,
            SkipNext = false,
            NextFireAt = result.NextFireAt,
        };
        await svc.UpsertScheduleAsync(row);
        return ctx.Ok(new ScheduleValidationResultDto(true, [], result.NextFireAt), CoveJsonContext.Default.ScheduleValidationResultDto);
    }

    [CoveCommand("cove://commands/task.repeat.get")]
    public static Task<ControlResponse> Get(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ScheduleGetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "repeat get params required (cardId)"));
        var row = svc.GetSchedule(p.CardId);
        if (row is null)
            return Task.FromResult(ctx.Fail("not_found", "no schedule for card"));
        return Task.FromResult(ctx.Ok(ToInfo(row), CoveJsonContext.Default.ScheduleInfo));
    }

    [CoveCommand("cove://commands/task.repeat.pause")]
    public static async Task<ControlResponse> Pause(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ScheduleUpdateStateParams) is not { } p)
            return ctx.Fail("invalid_params", "repeat pause params required (cardId)");
        var row = svc.GetSchedule(p.CardId);
        if (row is null)
            return ctx.Fail("not_found", "no schedule for card");
        await svc.UpdateScheduleAsync(p.CardId, paused: true, skipNext: null, nextFireAt: null, lastFiredAt: null);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/task.repeat.resume")]
    public static async Task<ControlResponse> Resume(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ScheduleUpdateStateParams) is not { } p)
            return ctx.Fail("invalid_params", "repeat resume params required (cardId)");
        var row = svc.GetSchedule(p.CardId);
        if (row is null)
            return ctx.Fail("not_found", "no schedule for card");
        var nextFireAt = Cove.Tasks.Schedules.NextFireAtCalculator.Compute(row.TriggerKind, row.Cron, row.At, row.Tz, CronExpander);
        await svc.UpdateScheduleAsync(p.CardId, paused: false, skipNext: null, nextFireAt: nextFireAt, lastFiredAt: null);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/task.repeat.skip-next")]
    public static async Task<ControlResponse> SkipNext(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ScheduleUpdateStateParams) is not { } p)
            return ctx.Fail("invalid_params", "repeat skip-next params required (cardId)");
        var row = svc.GetSchedule(p.CardId);
        if (row is null)
            return ctx.Fail("not_found", "no schedule for card");
        await svc.UpdateScheduleAsync(p.CardId, paused: null, skipNext: true, nextFireAt: null, lastFiredAt: null);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/task.repeat.stop")]
    public static async Task<ControlResponse> Stop(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ScheduleGetParams) is not { } p)
            return ctx.Fail("invalid_params", "repeat stop params required (cardId)");
        await svc.DeleteScheduleAsync(p.CardId);
        return ctx.Ok();
    }

    private static ScheduleInfo ToInfo(Cove.Tasks.Schedules.ScheduleRow row)
    {
        var mode = Cove.Tasks.Schedules.ScheduleModeResolver.DeriveMode(row);
        return new ScheduleInfo(row.CardId, row.TriggerKind, row.Cron, row.Tz, row.At, row.CompletionRule, row.MarkDoneBy, row.BlockOverlap, row.HomeStatusId, row.Paused, row.SkipNext, row.NextFireAt, row.LastFiredAt, mode);
    }
}
