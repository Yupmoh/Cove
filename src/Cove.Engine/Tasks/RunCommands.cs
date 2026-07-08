using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Tasks;

public static class RunCommands
{
    [CoveCommand("cove://commands/run.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.RunListParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "run list params required (taskId or workspaceId required)"));
        if (p.TaskId is null && p.WorkspaceId is null)
            return Task.FromResult(ctx.Fail("invalid_params", "either taskId or workspaceId is required"));
        var runs = svc.ListRuns(p.TaskId, p.WorkspaceId, p.State).Select(ToInfo).ToList();
        return Task.FromResult(ctx.Ok(new RunListResult(runs), CoveJsonContext.Default.RunListResult));
    }

    [CoveCommand("cove://commands/run.show")]
    public static Task<ControlResponse> Show(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.RunRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "run ref params required"));
        var run = svc.GetRun(p.Id);
        if (run is null)
            return Task.FromResult(ctx.Fail("not_found", "run not found"));
        return Task.FromResult(ctx.Ok(ToInfo(run), CoveJsonContext.Default.RunInfo));
    }

    [CoveCommand("cove://commands/run.segments")]
    public static Task<ControlResponse> Segments(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.RunRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "run ref params required"));
        var segments = svc.ListRunSegments(p.Id).Select(s => new RunSegmentInfo(s.Id, s.RunId, s.PaneId, s.AdapterSessionId, s.StartedAt, s.EndedAt)).ToList();
        return Task.FromResult(ctx.Ok(new RunSegmentListResult(segments), CoveJsonContext.Default.RunSegmentListResult));
    }

    [CoveCommand("cove://commands/run.cancel")]
    public static async Task<ControlResponse> Cancel(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.RunRefParams) is not { } p)
            return ctx.Fail("invalid_params", "run ref params required");
        try
        {
            await svc.TransitionRunAsync(p.Id, Cove.Tasks.Runs.RunState.Cancelled);
            return ctx.Ok();
        }
        catch (System.InvalidOperationException ex)
        {
            return ctx.Fail("invalid_transition", ex.Message);
        }
    }

    [CoveCommand("cove://commands/run.complete")]
    public static async Task<ControlResponse> Complete(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.RunCompleteParams) is not { } p)
            return ctx.Fail("invalid_params", "run complete params required");
        try
        {
            await svc.TransitionRunAsync(p.Id, Cove.Tasks.Runs.RunState.Completed);
            return ctx.Ok();
        }
        catch (System.InvalidOperationException ex)
        {
            return ctx.Fail("invalid_transition", ex.Message);
        }
    }

    [CoveCommand("cove://commands/run.set-pending-prompt")]
    public static async Task<ControlResponse> SetPendingPrompt(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.RunSetPendingPromptParams) is not { } p)
            return ctx.Fail("invalid_params", "run set-pending-prompt params required (id, prompt)");
        if (string.IsNullOrEmpty(p.Id))
            return ctx.Fail("invalid_params", "run id is required");
        var run = svc.GetRun(p.Id);
        if (run is null)
            return ctx.Fail("not_found", "run not found");
        await svc.SetPendingPromptAsync(p.Id, p.Prompt);
        return ctx.Ok();
    }

    private static RunInfo ToInfo(Cove.Tasks.Runs.RunRow row) =>
        new(row.Id, row.CardId, row.WorkspaceId, row.RunFamilyId, row.State, row.Backgrounded, row.LaunchProfileJson, row.PendingPrompt, row.StartedAt, row.EndedAt, row.CreatedAt);
}
