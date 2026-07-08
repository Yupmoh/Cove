using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Tasks;

public static class CommentCommands
{
    [CoveCommand("cove://commands/task.comment.add")]
    public static async Task<ControlResponse> Add(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.CommentAddParams) is not { } p)
            return ctx.Fail("invalid_params", "comment add params required");
        try
        {
            var row = await svc.AddCommentAsync(p.CardId, p.Kind, p.Body, p.Source);
            if (row is null)
                return ctx.Fail("not_found", "card not found");
            return ctx.Ok(ToInfo(row), CoveJsonContext.Default.CommentInfo);
        }
        catch (System.ArgumentException ex)
        {
            return ctx.Fail("invalid_kind", ex.Message);
        }
    }

    [CoveCommand("cove://commands/task.comment.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.CommentListParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "comment list params required"));
        var comments = svc.ListComments(p.CardId).Select(ToInfo).ToList();
        return Task.FromResult(ctx.Ok(new CommentListResult(comments), CoveJsonContext.Default.CommentListResult));
    }

    [CoveCommand("cove://commands/task.comment.count")]
    public static Task<ControlResponse> Count(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.CommentListParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "comment list params required"));
        return Task.FromResult(ctx.Ok(new CommentCountResult(svc.CountComments(p.CardId)), CoveJsonContext.Default.CommentCountResult));
    }

    [CoveCommand("cove://commands/task.comment.delete")]
    public static async Task<ControlResponse> Delete(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.CommentRefParams) is not { } p)
            return ctx.Fail("invalid_params", "comment ref params required");
        await svc.DeleteCommentAsync(p.Id);
        return ctx.Ok();
    }

    private static CommentInfo ToInfo(Cove.Tasks.Store.CommentRow row) =>
        new(row.Id, row.CardId, row.Kind, row.Body, row.Source, row.CreatedAt);
}
