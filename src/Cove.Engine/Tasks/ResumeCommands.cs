using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Tasks;

public static class ResumeCommands
{
    [CoveCommand("cove://commands/run.resume")]
    public static async Task<ControlResponse> Resume(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.RunResumeParams) is not { } p)
            return ctx.Fail("invalid_params", "run resume params required (id, optional nookId/adapterOverride)");
        if (string.IsNullOrEmpty(p.Id))
            return ctx.Fail("invalid_params", "run id is required");
        var saga = ctx.ResumeSaga;
        if (saga is null)
            return ctx.Fail("not_ready", "resume saga not configured (requires M1/M3 services)");
        var result = await saga.ResumeAsync(p.Id, p.NookId, p.AdapterOverride);
        return ctx.Ok(new RunResumeResult(result.Success, result.NewSegmentId, result.Error, result.Outcome.ToString()), CoveJsonContext.Default.RunResumeResult);
    }
}
