using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Tasks;

public static class LabelCommands
{
    [CoveCommand("cove://commands/task.label.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LabelListParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "label list params required"));
        var labels = svc.ListLabels(p.WorkspaceId).Select(ToInfo).ToList();
        return Task.FromResult(ctx.Ok(new LabelListResult(labels), CoveJsonContext.Default.LabelListResult));
    }

    [CoveCommand("cove://commands/task.label.create")]
    public static async Task<ControlResponse> Create(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LabelCreateParams) is not { } p)
            return ctx.Fail("invalid_params", "label create params required");
        var row = await svc.CreateLabelAsync(p.WorkspaceId, p.Id, p.Name, p.HexColor, p.Position);
        if (row is null)
            return ctx.Fail("conflict", "label with that id or name already exists");
        return ctx.Ok(ToInfo(row), CoveJsonContext.Default.LabelInfo);
    }

    [CoveCommand("cove://commands/task.label.delete")]
    public static async Task<ControlResponse> Delete(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LabelRefParams) is not { } p)
            return ctx.Fail("invalid_params", "label ref params required");
        await svc.DeleteLabelAsync(p.WorkspaceId, p.Id);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/task.label.assign")]
    public static async Task<ControlResponse> Assign(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LabelAssignParams) is not { } p)
            return ctx.Fail("invalid_params", "label assign params required");
        await svc.AssignLabelAsync(p.CardId, p.LabelId);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/task.label.unassign")]
    public static async Task<ControlResponse> Unassign(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LabelAssignParams) is not { } p)
            return ctx.Fail("invalid_params", "label assign params required");
        await svc.UnassignLabelAsync(p.CardId, p.LabelId);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/task.label.reorder")]
    public static async Task<ControlResponse> Reorder(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LabelReorderParams) is not { } p)
            return ctx.Fail("invalid_params", "label reorder params required");
        await svc.ReorderLabelsAsync(p.WorkspaceId, p.OrderedIds);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/task.label.filter")]
    public static Task<ControlResponse> Filter(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LabelFilterParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "label filter params required"));
        var cardIds = svc.FilterCardsByLabel(p.WorkspaceId, p.LabelId);
        return Task.FromResult(ctx.Ok(new LabelFilterResult(cardIds), CoveJsonContext.Default.LabelFilterResult));
    }

    private static LabelInfo ToInfo(Cove.Tasks.Store.LabelRow row) =>
        new(row.WorkspaceId, row.Id, row.Name, row.HexColor, row.Position);
}
