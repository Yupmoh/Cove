using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Tasks;

public static class StatusCommands
{
    [CoveCommand("cove://commands/task.status.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.StatusListParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "status list params required"));
        var statuses = svc.ListStatuses(p.BayId, includeHidden: true).Select(ToInfo).ToList();
        return Task.FromResult(ctx.Ok(new StatusListResult(statuses), CoveJsonContext.Default.StatusListResult));
    }

    [CoveCommand("cove://commands/task.status.create")]
    public static async Task<ControlResponse> Create(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.StatusCreateParams) is not { } p)
            return ctx.Fail("invalid_params", "status create params required");
        var row = await svc.CreateStatusAsync(p.BayId, p.Id, p.Name, p.HexColor, p.Position);
        if (row is null)
            return ctx.Fail("conflict", "status with that id or name already exists");
        return ctx.Ok(ToInfo(row), CoveJsonContext.Default.StatusInfo);
    }

    [CoveCommand("cove://commands/task.status.delete")]
    public static async Task<ControlResponse> Delete(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.StatusDeleteParams) is not { } p)
            return ctx.Fail("invalid_params", "status delete params required");
        try
        {
            await svc.DeleteStatusAsync(p.BayId, p.Id, p.RehomeToStatusId);
            return ctx.Ok();
        }
        catch (System.InvalidOperationException ex)
        {
            return ctx.Fail("conflict", ex.Message);
        }
    }

    [CoveCommand("cove://commands/task.status.reorder")]
    public static async Task<ControlResponse> Reorder(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.StatusReorderParams) is not { } p)
            return ctx.Fail("invalid_params", "status reorder params required");
        await svc.ReorderStatusesAsync(p.BayId, p.OrderedIds);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/task.status.set-hidden")]
    public static async Task<ControlResponse> SetHidden(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.StatusSetHiddenParams) is not { } p)
            return ctx.Fail("invalid_params", "status set-hidden params required");
        await svc.SetStatusHiddenAsync(p.BayId, p.Id, p.Hidden);
        return ctx.Ok();
    }

    private static StatusInfo ToInfo(Cove.Tasks.Store.StatusRow row) => new(
        row.BayId, row.Id, row.Name, row.HexColor, row.Position, row.Hidden, row.IsLooping, row.IsInProgress, row.IsReview, row.IsCompletion);
}
