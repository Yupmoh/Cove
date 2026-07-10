using System.Text.Json;
using Cove.Protocol;

namespace Cove.Engine;

internal static class EngineCommands
{
    [CoveCommand("cove://commands/pane.list")]
    public static Task<ControlResponse> PaneList(EngineDispatchContext ctx)
    {
        PaneInfo[] panes = ctx.Panes is { } reg ? reg.List() : Array.Empty<PaneInfo>();
        return Task.FromResult(ctx.Ok(new PaneListResult(panes), CoveJsonContext.Default.PaneListResult));
    }

    [CoveCommand("cove://commands/pane.spawn")]
    public static Task<ControlResponse> PaneSpawn(EngineDispatchContext ctx)
    {
        if (ctx.Panes is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.SpawnParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "spawn params required"));
        PaneInfo info = reg.Spawn(p);
        if (ctx.AgentRouter is { } router && p.Adapter is { } adapter)
            router.Register(info.PaneId, adapter, p.AgentName, p.Workspace, p.Room, mcpAccessScope: p.McpAccessScope, mcpVisible: p.McpVisible);
        return Task.FromResult(ctx.Ok(info, CoveJsonContext.Default.PaneInfo));
    }

    [CoveCommand("cove://commands/pane.write")]
    public static Task<ControlResponse> PaneWrite(EngineDispatchContext ctx)
    {
        if (ctx.Panes is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneWriteParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "write params required"));
        var resolved = reg.ResolveId(p.PaneId);
        if (!resolved.Found)
            return Task.FromResult(ctx.Fail(resolved.ErrorCode ?? "not_found", $"unknown pane {p.PaneId}"));
        byte[] data = Convert.FromBase64String(p.DataBase64);
        return Task.FromResult(reg.Write(resolved.Id!, data)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"unknown pane {p.PaneId}"));
    }

    [CoveCommand("cove://commands/pane.resize")]
    public static Task<ControlResponse> PaneResize(EngineDispatchContext ctx)
    {
        if (ctx.Panes is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ResizeParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "resize params required"));
        var resolved = reg.ResolveId(p.PaneId);
        if (!resolved.Found)
            return Task.FromResult(ctx.Fail(resolved.ErrorCode ?? "not_found", $"unknown pane {p.PaneId}"));
        return Task.FromResult(reg.Resize(resolved.Id!, p.Cols, p.Rows)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"unknown pane {p.PaneId}"));
    }

    [CoveCommand("cove://commands/pane.kill")]
    public static Task<ControlResponse> PaneKill(EngineDispatchContext ctx)
    {
        if (ctx.Panes is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));
        var resolved = reg.ResolveId(p.PaneId);
        if (!resolved.Found)
            return Task.FromResult(ctx.Fail(resolved.ErrorCode ?? "not_found", $"unknown pane {p.PaneId}"));
        ctx.AgentRouter?.Unregister(resolved.Id!);
        return Task.FromResult(reg.Kill(resolved.Id!)
                ? ctx.Ok()
                : ctx.Fail("not_found", $"unknown pane {p.PaneId}"));
    }

    [CoveCommand("cove://commands/pane.search")]
    public static Task<ControlResponse> PaneSearch(EngineDispatchContext ctx)
    {
        if (ctx.Panes is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.SearchParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "search params required"));
        var resolved = reg.ResolveId(p.PaneId);
        var searchPaneId = resolved.Found ? resolved.Id! : p.PaneId;
        var matches = reg.Search(searchPaneId, p.Query, p.CaseSensitive);
        return Task.FromResult(ctx.Ok(new SearchResult(matches), CoveJsonContext.Default.SearchResult));
    }

    [CoveCommand("cove://commands/pane.rename")]
    public static Task<ControlResponse> PaneRename(EngineDispatchContext ctx)
    {
        if (ctx.Panes is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRenameParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "rename params required"));
        var resolved = reg.ResolveId(p.PaneId);
        if (!resolved.Found)
            return Task.FromResult(ctx.Fail(resolved.ErrorCode ?? "not_found", $"unknown pane {p.PaneId}"));
        return Task.FromResult(reg.Rename(resolved.Id!, p.Title)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"unknown pane {p.PaneId}"));
    }

    [CoveCommand("cove://commands/pane.read")]
    public static Task<ControlResponse> PaneRead(EngineDispatchContext ctx)
    {
        if (ctx.Panes is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneReadParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "read params required"));
        var resolved = reg.ResolveId(p.PaneId);
        var readPaneId = resolved.Found ? resolved.Id! : p.PaneId;
        byte[] bytes = reg.Read(readPaneId, p.Offset, p.MaxBytes);
        long head = 0;
        if (reg.TryGet(readPaneId, out var pane))
            head = pane.Ring.Head;
        long nextOffset = p.Offset + bytes.Length;
        return Task.FromResult(ctx.Ok(new PaneReadResult(System.Convert.ToBase64String(bytes), nextOffset, head), CoveJsonContext.Default.PaneReadResult));
    }
}
