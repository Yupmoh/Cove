using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Tasks;

public static class DispatchCommands
{
    [CoveCommand("cove://commands/task.launch")]
    public static async Task<ControlResponse> Launch(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskLaunchParams) is not { } p)
            return ctx.Fail("invalid_params", "task launch params required");
        var card = svc.GetCard(p.CardId);
        if (card is null)
            return ctx.Fail("not_found", "card not found");
        if (svc.HasActiveRun(p.CardId))
            return ctx.Fail("conflict", "card already has an active or interrupted run");
        var saga = ctx.DispatchSaga;
        if (saga is null)
            return ctx.Fail("not_ready", "dispatch saga not configured (requires M1/M2/M3 services)");
        var result = await saga.LaunchAsync(p.CardId, card.WorkspaceId, p.ExecutionModeOverride);
        return ctx.Ok(new TaskLaunchResult(result.Success, result.RunId, result.Error, result.ReachedStep.ToString()), CoveJsonContext.Default.TaskLaunchResult);
    }
}
