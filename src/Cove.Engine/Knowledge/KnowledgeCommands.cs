using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Knowledge;

public static class KnowledgeCommands
{
    [CoveCommand("cove://commands/note.create")]
    public static Task<ControlResponse> NoteCreate(EngineDispatchContext ctx)
    {
        if (ctx.Notes is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteCreateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note create params required"));

        var note = store.Create(new Note { Title = p.Title, Content = p.Content ?? "", WorkspaceId = p.WorkspaceId, Source = p.Source, Kind = p.Kind ?? "markdown" });
        return Task.FromResult(ctx.Ok(note, CoveJsonContext.Default.Note));
    }

    [CoveCommand("cove://commands/note.get")]
    public static Task<ControlResponse> NoteGet(EngineDispatchContext ctx)
    {
        if (ctx.Notes is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note ref params required"));

        var note = store.Get(p.Id);
        if (note is null)
            return Task.FromResult(ctx.Fail("not_found", "note not found"));
        return Task.FromResult(ctx.Ok(note, CoveJsonContext.Default.Note));
    }

    [CoveCommand("cove://commands/note.list")]
    public static Task<ControlResponse> NoteList(EngineDispatchContext ctx)
    {
        if (ctx.Notes is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteListParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note list params required"));

        var notes = store.ListByWorkspace(p.WorkspaceId);
        return Task.FromResult(ctx.Ok(new NoteListResult(notes), CoveJsonContext.Default.NoteListResult));
    }

    [CoveCommand("cove://commands/note.update")]
    public static Task<ControlResponse> NoteUpdate(EngineDispatchContext ctx)
    {
        if (ctx.Notes is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteUpdateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note update params required"));

        store.Update(p.Id, n => n with { Title = p.Title ?? n.Title, Content = p.Content ?? n.Content });
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/note.delete")]
    public static Task<ControlResponse> NoteDelete(EngineDispatchContext ctx)
    {
        if (ctx.Notes is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note ref params required"));

        store.Delete(p.Id);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/timeline.append")]
    public static Task<ControlResponse> TimelineAppend(EngineDispatchContext ctx)
    {
        if (ctx.Timeline is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "timeline store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TimelineAppendParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "timeline append params required"));

        var entry = store.Append(new TimelineEntry { WorkspaceId = p.WorkspaceId, Kind = p.Kind, Source = p.Source, Scope = p.Scope, Summary = p.Summary });
        return Task.FromResult(ctx.Ok(entry, CoveJsonContext.Default.TimelineEntry));
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
}
