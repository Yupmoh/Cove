using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Knowledge;

public static class KnowledgeCommands
{
    [CoveCommand("cove://commands/knowledge.ping")]
    public static Task<ControlResponse> Ping(EngineDispatchContext ctx)
    {
        string? echo = null;
        if (ctx.Request.Params is JsonElement el && el.Deserialize(CoveJsonContext.Default.KnowledgePingParams) is { } p)
            echo = p.Echo;
        return Task.FromResult(ctx.Ok(new KnowledgePingResult("pong", echo), CoveJsonContext.Default.KnowledgePingResult));
    }

    [CoveCommand("cove://commands/note.create")]
    public static Task<ControlResponse> NoteCreate(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteCreateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note create params required"));

        var note = store.Create(new Note { Title = p.Title, Content = p.Content ?? "", WorkspaceId = p.WorkspaceId, Source = p.Source, Kind = p.Kind ?? "markdown" });
        return Task.FromResult(ctx.Ok(note, CoveJsonContext.Default.Note));
    }

    [CoveCommand("cove://commands/note.get")]
    public static Task<ControlResponse> NoteGet(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteReadParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note read params required"));

        var note = store.Get(p.WorkspaceId, p.Id);
        if (note is null)
            return Task.FromResult(ctx.Fail("not_found", "note not found"));
        return Task.FromResult(ctx.Ok(note, CoveJsonContext.Default.Note));
    }

    [CoveCommand("cove://commands/note.list")]
    public static Task<ControlResponse> NoteList(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteListParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note list params required"));

        var metas = store.ListByWorkspace(p.WorkspaceId);
        var notes = new System.Collections.Generic.List<Note>(metas.Count);
        foreach (var m in metas)
        {
            var full = store.Get(m.WorkspaceId, m.Id);
            if (full is not null)
                notes.Add(full);
        }
        return Task.FromResult(ctx.Ok(new NoteListResult(notes), CoveJsonContext.Default.NoteListResult));
    }

    [CoveCommand("cove://commands/note.update")]
    public static Task<ControlResponse> NoteUpdate(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteWriteParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note write params required"));

        store.Update(p.WorkspaceId, p.Id, n => n with { Title = p.Title ?? n.Title, Content = p.Content ?? n.Content, Kind = p.Kind ?? n.Kind });
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/note.delete")]
    public static Task<ControlResponse> NoteDelete(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteReadParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note read params required"));

        store.Delete(p.WorkspaceId, p.Id);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/note.search")]
    public static Task<ControlResponse> NoteSearch(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteSearchParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note search params required"));

        var results = store.Search(p.WorkspaceId, p.Query, p.Limit ?? 20);
        return Task.FromResult(ctx.Ok(new NoteSearchResult(results), CoveJsonContext.Default.NoteSearchResult));
    }

    [CoveCommand("cove://commands/note.read")]
    public static Task<ControlResponse> NoteRead(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteReadParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note read params required"));

        var note = store.Get(p.WorkspaceId, p.Id);
        if (note is null)
            return Task.FromResult(ctx.Fail("not_found", "note not found"));

        var format = p.Format;
        if (format == "svg")
        {
            if (note.Kind != "sketch")
                return Task.FromResult(ctx.Fail("invalid_format", "--svg is only valid for sketch notes"));
            var svgSerializer = new SketchSvgSerializer(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            var svg = svgSerializer.Serialize(note.Content);
            return Task.FromResult(ctx.Ok(new NoteReadResult(note.Id, note.Title, svg, note.Kind, "svg"), CoveJsonContext.Default.NoteReadResult));
        }
        if (format == "png")
        {
            if (note.Kind != "sketch")
                return Task.FromResult(ctx.Fail("invalid_format", "--png is only valid for sketch notes"));
            return Task.FromResult(ctx.Fail("not_supported", "PNG rasterization requires an image package not yet added — use --svg instead"));
        }

        return Task.FromResult(ctx.Ok(new NoteReadResult(note.Id, note.Title, note.Content, note.Kind, null), CoveJsonContext.Default.NoteReadResult));
    }

    [CoveCommand("cove://commands/note.write")]
    public static Task<ControlResponse> NoteWrite(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteWriteParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note write params required"));

        store.Update(p.WorkspaceId, p.Id, n => n with { Title = p.Title ?? n.Title, Content = p.Content ?? n.Content, Kind = p.Kind ?? n.Kind });
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/note.history")]
    public static Task<ControlResponse> NoteHistory(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteHistoryParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note history params required"));

        var history = store.GetHistory(p.WorkspaceId, p.Id);
        return Task.FromResult(ctx.Ok(new NoteHistoryResult(history), CoveJsonContext.Default.NoteHistoryResult));
    }

    [CoveCommand("cove://commands/note.media.save")]
    public static Task<ControlResponse> NoteMediaSave(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteMediaSaveParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note media save params required"));

        byte[] bytes;
        try
        {
            bytes = System.Convert.FromBase64String(p.Base64Data);
        }
        catch (System.FormatException)
        {
            return Task.FromResult(ctx.Fail("invalid_data", "base64 data is malformed or contains a data-URL prefix"));
        }
        var mediaPath = store.SaveMedia(p.WorkspaceId, p.Id, p.FileName, bytes);
        return Task.FromResult(ctx.Ok(new NoteMediaSaveResult(mediaPath), CoveJsonContext.Default.NoteMediaSaveResult));
    }

    [CoveCommand("cove://commands/note.get-state")]
    public static Task<ControlResponse> NoteGetState(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteGetStateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note get-state params required"));

        var state = store.LoadState(p.WorkspaceId, p.Id);
        return Task.FromResult(ctx.Ok(new NoteGetStateResult(state), CoveJsonContext.Default.NoteGetStateResult));
    }

    [CoveCommand("cove://commands/note.save-state")]
    public static Task<ControlResponse> NoteSaveState(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteSaveStateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note save-state params required"));

        store.SaveState(p.WorkspaceId, p.Id, p.StateJson);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/canvas.action")]
    public static async Task<ControlResponse> CanvasAction(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.CanvasActionParams) is not { } p)
            return ctx.Fail("invalid_params", "canvas action params required");

        if (p.Action == "cove_command")
        {
            if (string.IsNullOrEmpty(p.Uri))
                return ctx.Fail("invalid_params", "uri required for cove_command action");
            if (ctx.Redrive is null)
                return ctx.Fail("not_ready", "redrive unavailable");
            var subReq = new ControlRequest(ctx.Request.Id + "-action", p.Uri, p.State);
            var subResp = await ctx.Redrive(subReq);
            if (subResp is null)
                return ctx.Fail("not_found", $"command '{p.Uri}' not found");
            return ctx.Ok(new CanvasActionResult(true, null, p.Uri), CoveJsonContext.Default.CanvasActionResult);
        }

        if (p.Action == "send_to_agent")
        {
            if (string.IsNullOrEmpty(p.TargetPane))
                return ctx.Fail("invalid_params", "targetPane required for send_to_agent action");
            if (ctx.Panes is not { } panes)
                return ctx.Fail("not_ready", "pane registry not available");
            var message = p.Payload ?? "";
            var bytes = System.Text.Encoding.UTF8.GetBytes(message + "\r");
            if (!panes.Write(p.TargetPane, bytes))
                return ctx.Fail("not_found", $"target pane {p.TargetPane} not found or write failed");
            return ctx.Ok(new CanvasActionResult(true, p.TargetPane, null), CoveJsonContext.Default.CanvasActionResult);
        }

        return ctx.Fail("invalid_action", $"unknown action: {p.Action}");
    }

    [CoveCommand("cove://commands/timeline.append")]
    public static Task<ControlResponse> TimelineAppend(EngineDispatchContext ctx)
    {
        if (ctx.Timeline is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "timeline store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TimelineAppendParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "timeline append params required"));

        try
        {
            var entry = store.Append(new TimelineEntry { WorkspaceId = p.WorkspaceId, Kind = p.Kind, Source = p.Source, Scope = p.Scope, Summary = p.Summary, Tags = p.Tags });
            return Task.FromResult(ctx.Ok(entry, CoveJsonContext.Default.TimelineEntry));
        }
        catch (TimelineValidationException ex)
        {
            return Task.FromResult(ctx.Fail(ex.Code, ex.Message));
        }
    }

    [CoveCommand("cove://commands/timeline.list")]
    public static Task<ControlResponse> TimelineList(EngineDispatchContext ctx)
    {
        if (ctx.Timeline is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "timeline store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TimelineListParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "timeline list params required"));

        var entries = store.ListByWorkspace(p.WorkspaceId);
        return Task.FromResult(ctx.Ok(new TimelineListResult(entries), CoveJsonContext.Default.TimelineListResult));
    }

    [CoveCommand("cove://commands/blackboard.post")]
    public static Task<ControlResponse> BlackboardPost(EngineDispatchContext ctx)
    {
        if (ctx.Blackboard is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "blackboard store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BlackboardPostParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "blackboard post params required"));

        System.TimeSpan? ttl = p.TtlSeconds.HasValue ? System.TimeSpan.FromSeconds(p.TtlSeconds.Value) : null;
        var post = store.Post(p.WorkspaceId, p.Kind, p.Audience, p.Content, p.RefId, ttl);
        return Task.FromResult(ctx.Ok(post, CoveJsonContext.Default.BlackboardPost));
    }

    [CoveCommand("cove://commands/blackboard.show")]
    public static Task<ControlResponse> BlackboardShow(EngineDispatchContext ctx)
    {
        if (ctx.Blackboard is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "blackboard store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BlackboardShowParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "blackboard show params required"));

        var posts = store.Show(p.WorkspaceId, p.Audience);
        return Task.FromResult(ctx.Ok(new BlackboardShowResult(posts), CoveJsonContext.Default.BlackboardShowResult));
    }
}
