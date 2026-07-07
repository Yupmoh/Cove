using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Tasks;

public static class TaskCommands
{
    [CoveCommand("cove://commands/task.create")]
    public static Task<ControlResponse> Create(EngineDispatchContext ctx)
    {
        if (ctx.Tasks is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskCreateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "task create params required"));

        var card = store.Create(new TaskCard
        {
            Title = p.Title,
            Description = p.Description ?? "",
            WorkspaceId = p.WorkspaceId,
            Source = p.Source,
            Priority = ParsePriority(p.Priority),
            Size = ParseSize(p.Size),
            Assignee = p.Assignee,
        });
        return Task.FromResult(ctx.Ok(card, CoveJsonContext.Default.TaskCard));
    }

    [CoveCommand("cove://commands/task.get")]
    public static Task<ControlResponse> Get(EngineDispatchContext ctx)
    {
        if (ctx.Tasks is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "task ref params required"));

        var card = p.HumanId is not null ? store.ResolveByHumanId(p.HumanId) : store.Get(p.Id ?? "");
        if (card is null)
            return Task.FromResult(ctx.Fail("not_found", "task not found"));
        return Task.FromResult(ctx.Ok(card, CoveJsonContext.Default.TaskCard));
    }

    [CoveCommand("cove://commands/task.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.Tasks is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskListParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "task list params required"));

        var cards = store.ListByWorkspace(p.WorkspaceId);
        return Task.FromResult(ctx.Ok(new TaskListResult(cards), CoveJsonContext.Default.TaskListResult));
    }

    [CoveCommand("cove://commands/task.update")]
    public static Task<ControlResponse> Update(EngineDispatchContext ctx)
    {
        if (ctx.Tasks is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskUpdateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "task update params required"));

        store.Update(p.Id, c => c with
        {
            Title = p.Title ?? c.Title,
            StatusId = p.StatusId ?? c.StatusId,
            Description = p.Description ?? c.Description,
            Assignee = p.Assignee ?? c.Assignee,
        });
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/task.delete")]
    public static Task<ControlResponse> Delete(EngineDispatchContext ctx)
    {
        if (ctx.Tasks is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "task ref params required"));

        store.Delete(p.Id ?? "");
        return Task.FromResult(ctx.Ok());
    }

    private static TaskPriority ParsePriority(string? p) => p?.ToLowerInvariant() switch
    {
        "critical" => TaskPriority.Critical,
        "high" => TaskPriority.High,
        "low" => TaskPriority.Low,
        _ => TaskPriority.Medium,
    };

    private static TaskSize ParseSize(string? s) => s?.ToLowerInvariant() switch
    {
        "xs" => TaskSize.Xs,
        "s" => TaskSize.S,
        "l" => TaskSize.L,
        "xl" => TaskSize.Xl,
        _ => TaskSize.M,
    };
}
