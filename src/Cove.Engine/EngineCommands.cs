using System;
using System.Text.Json;
using System.Threading.Tasks;
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
        return Task.FromResult(ctx.Ok(info, CoveJsonContext.Default.PaneInfo));
    }

    [CoveCommand("cove://commands/pane.write")]
    public static Task<ControlResponse> PaneWrite(EngineDispatchContext ctx)
    {
        if (ctx.Panes is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneWriteParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "write params required"));
        byte[] data = Convert.FromBase64String(p.DataBase64);
        return Task.FromResult(reg.Write(p.PaneId, data)
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
        return Task.FromResult(reg.Resize(p.PaneId, p.Cols, p.Rows)
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
        return Task.FromResult(reg.Kill(p.PaneId)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"unknown pane {p.PaneId}"));
    }
}
