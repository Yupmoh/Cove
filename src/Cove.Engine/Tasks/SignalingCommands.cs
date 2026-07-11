using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Tasks;

public static class SignalingCommands
{
    [CoveCommand("cove://commands/task.set-in-review")]
    public static async Task<ControlResponse> SetInReview(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskSetInReviewParams) is not { } p)
            return ctx.Fail("invalid_params", "set-in-review params required");
        var resolution = ResolveRun(svc, ctx, p.RunId, p.BayId);
        if (!resolution.Found)
            return ctx.Fail("not_found", resolution.Error ?? "could not resolve run");
        var run = resolution.Run!;
        if (run.ReviewStatusId is null)
            return ctx.Fail("no_review_status", $"run {run.Id} was launched without a review status gate (default-launched)");
        var card = svc.GetCard(run.CardId);
        if (card is null)
            return ctx.Fail("not_found", "card not found");
        card.StatusId = run.ReviewStatusId;
        await svc.UpdateCardAsync(card);
        return ctx.Ok(ToCard(card), CoveJsonContext.Default.TaskCard);
    }

    [CoveCommand("cove://commands/task.set-done")]
    public static async Task<ControlResponse> SetDone(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskSetDoneParams) is not { } p)
            return ctx.Fail("invalid_params", "set-done params required");
        var resolution = ResolveRun(svc, ctx, p.RunId, p.BayId);
        if (!resolution.Found)
            return ctx.Fail("not_found", resolution.Error ?? "could not resolve run");
        var run = resolution.Run!;
        if (run.CompletionStatusId is null)
            return ctx.Fail("no_completion_status", $"run {run.Id} was launched without a completion status gate (default-launched)");
        var card = svc.GetCard(run.CardId);
        if (card is null)
            return ctx.Fail("not_found", "card not found");
        card.StatusId = run.CompletionStatusId;
        await svc.UpdateCardAsync(card);
        try { await svc.TransitionRunAsync(run.Id, Cove.Tasks.Runs.RunState.Completed); }
        catch (System.InvalidOperationException ex) { return ctx.Fail("invalid_transition", ex.Message); }
        return ctx.Ok(ToCard(card), CoveJsonContext.Default.TaskCard);
    }

    [CoveCommand("cove://commands/task.claim")]
    public static async Task<ControlResponse> Claim(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskClaimParams) is not { } p)
            return ctx.Fail("invalid_params", "task claim params required (cardId)");
        var card = svc.GetCard(p.CardId);
        if (card is null)
            return ctx.Fail("not_found", "card not found");
        if (svc.HasActiveRun(p.CardId))
            return ctx.Fail("conflict", "card already has an active or interrupted run");
        var config = card.LaunchConfigJson is not null
            ? Cove.Tasks.LaunchConfig.LaunchConfigSerializer.Deserialize(card.LaunchConfigJson)
            : null;
        var run = await svc.CreateRunAsync(p.CardId, card.BayId, card.LaunchConfigJson, reviewStatusId: config?.ReviewStatusId, completionStatusId: config?.CompletionStatusId);
        var nookId = p.NookId ?? ctx.Request.CallerNookId;
        if (nookId is not null)
            await svc.AddRunSegmentAsync(run!.Id, nookId, null);
        return ctx.Ok(new TaskClaimResult(true, run!.Id, null), CoveJsonContext.Default.TaskClaimResult);
    }

    private sealed record RunResolution(Cove.Tasks.Runs.RunRow? Run, string? Error)
    {
        public bool Found => Run is not null;
    }

    private static RunResolution ResolveRun(Cove.Tasks.TaskService svc, EngineDispatchContext ctx, string? runId, string? bayId)
    {
        if (runId is not null)
        {
            if (TryParseHumanId(runId, out var number))
            {
                if (bayId is null)
                    return new RunResolution(null, "COVE-N task id requires --bay (or run from a bay-bound nook)");
                var card = svc.GetCardByHumanId(bayId, number);
                if (card is null)
                    return new RunResolution(null, $"no card COVE-{number} in bay {bayId}");
                var run = svc.GetActiveRunForCard(card.Id);
                if (run is null)
                    return new RunResolution(null, $"card COVE-{number} has no active or interrupted run");
                return new RunResolution(run, null);
            }
            var exact = svc.GetRun(runId);
            if (exact is not null) return new RunResolution(exact, null);
            var prefixed = svc.FindRunByPrefix(runId);
            if (prefixed is not null) return new RunResolution(prefixed, null);
            return new RunResolution(null, $"no run matching {runId}");
        }
        if (ctx.Request.CallerNookId is { } nookId)
        {
            var run = svc.GetRunByNook(nookId);
            if (run is not null) return new RunResolution(run, null);
            return new RunResolution(null, $"no run bound to nook {nookId}");
        }
        return new RunResolution(null, "pass --run-id (task id | UUID | prefix) or run from a bound nook");
    }

    private static bool TryParseHumanId(string humanId, out int number)
    {
        const string prefix = "COVE-";
        if (humanId.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            return int.TryParse(humanId[prefix.Length..], out number);
        number = 0;
        return false;
    }
    private static TaskCard ToCard(Cove.Tasks.Store.CardRow row) => new()
    {
        Id = row.Id,
        Title = row.Title,
        Description = row.Description,
        StatusId = row.StatusId,
        Priority = (TaskPriority)row.Priority,
        Size = (TaskSize)row.Size,
        Assignee = row.Assignee,
        Source = row.Source,
        BayId = row.BayId,
        TaskNumber = row.TaskNumber,
        CurrentPrimaryRunId = row.CurrentPrimaryRunId,
        CreatedAt = System.DateTimeOffset.Parse(row.CreatedAt),
        UpdatedAt = System.DateTimeOffset.Parse(row.UpdatedAt),
    };
}
