using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Tasks;

public static class TaskCommands
{
    [CoveCommand("cove://commands/task.create")]
    public static async Task<ControlResponse> Create(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskCreateParams) is not { } p)
            return ctx.Fail("invalid_params", "task create params required");

        var row = await svc.CreateCardAsync(p.BayId, p.Title, p.Source, p.Description, (int)ParsePriority(p.Priority), (int)ParseSize(p.Size), p.Assignee);
        return ctx.Ok(ToCard(row), CoveJsonContext.Default.TaskCard);
    }

    [CoveCommand("cove://commands/task.get")]
    public static Task<ControlResponse> Get(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "task ref params required"));

        Cove.Tasks.Store.CardRow? row;
        if (p.HumanId is not null && TryParseHumanId(p.HumanId, out var number))
            row = svc.GetCardByHumanId(p.BayId ?? "", number);
        else
            row = svc.GetCard(p.Id ?? "");

        if (row is null)
            return Task.FromResult(ctx.Fail("not_found", "task not found"));
        return Task.FromResult(ctx.Ok(ToCard(row), CoveJsonContext.Default.TaskCard));
    }

    [CoveCommand("cove://commands/task.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskListParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "task list params required"));

        var cards = svc.ListCards(p.BayId).Select(ToCard).ToList();
        return Task.FromResult(ctx.Ok(new TaskListResult(cards), CoveJsonContext.Default.TaskListResult));
    }

    [CoveCommand("cove://commands/task.update")]
    public static async Task<ControlResponse> Update(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskUpdateParams) is not { } p)
            return ctx.Fail("invalid_params", "task update params required");

        var row = svc.GetCard(p.Id);
        if (row is null)
            return ctx.Fail("not_found", "task not found");

        if (p.Title is not null) row.Title = p.Title;
        if (p.StatusId is not null) row.StatusId = p.StatusId;
        if (p.Description is not null) row.Description = p.Description;
        if (p.Assignee is not null) row.Assignee = p.Assignee;
        if (p.Source is not null) row.Source = p.Source;

        await svc.UpdateCardAsync(row);
        return ctx.Ok(ToCard(row), CoveJsonContext.Default.TaskCard);
    }

    [CoveCommand("cove://commands/task.delete")]
    public static async Task<ControlResponse> Delete(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskRefParams) is not { } p)
            return ctx.Fail("invalid_params", "task ref params required");

        await svc.DeleteCardAsync(p.Id ?? "");
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/task.ping", Description = "echo back params as pong (smoke)")]
    public static Task<ControlResponse> Ping(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskPingParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "task ping params required"));
        var result = new TaskPingResult(p.Echo, p.Kind, "pong");
        return Task.FromResult(ctx.Ok(result, CoveJsonContext.Default.TaskPingResult));
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

    private static bool TryParseHumanId(string humanId, out int number)
    {
        const string prefix = "COVE-";
        if (humanId.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            return int.TryParse(humanId[prefix.Length..], out number);
        number = 0;
        return false;
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
