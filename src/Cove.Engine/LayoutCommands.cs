using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Cove.Engine.Layout;
using Cove.Persistence;
using Cove.Protocol;

namespace Cove.Engine;

internal static class LayoutCommands
{
    [CoveCommand("cove://commands/layout.get")]
    public static Task<ControlResponse> LayoutGet(EngineDispatchContext ctx)
    {
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        var snap = layout.ToSnapshot("default", "default", Environment.CurrentDirectory);
        return Task.FromResult(ctx.Ok(snap, Cove.Persistence.CoveJsonContext.Default.WorkspaceSnapshot));
    }

    [CoveCommand("cove://commands/layout.snapshot")]
    public static Task<ControlResponse> LayoutSnapshot(EngineDispatchContext ctx)
    {
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        var snap = layout.ToSnapshot("default", "default", Environment.CurrentDirectory);
        return Task.FromResult(ctx.Ok(snap, Cove.Persistence.CoveJsonContext.Default.WorkspaceSnapshot));
    }

    [CoveCommand("cove://commands/layout.mutate")]
    public static Task<ControlResponse> LayoutMutate(EngineDispatchContext ctx)
    {
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(Cove.Protocol.CoveJsonContext.Default.LayoutMutateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "mutate params required"));

        try
        {
            return Task.FromResult(p.Op switch
            {
                "createRoom" => ctx.Ok(new LayoutMutateResult(layout.CreateRoom(p.Name ?? "main", NewLeaf(p.NewPaneId!))), Cove.Protocol.CoveJsonContext.Default.LayoutMutateResult),
                "split" => MutateOk(() => layout.SplitPane(p.RoomId!, p.TargetPaneId!, Orient(p.Orientation), NewLeaf(p.NewPaneId!)), p.RoomId, ctx),
                "close" => MutateOk(() => layout.ClosePane(p.RoomId!, p.PaneId!), p.RoomId, ctx),
                "addSubtab" => MutateOk(() => layout.AddSubtab(p.RoomId!, p.PaneId!, p.NewPaneId!), p.RoomId, ctx),
                "activateSubtab" => MutateOk(() => layout.ActivateSubtab(p.RoomId!, p.PaneId!, p.Dir), p.RoomId, ctx),
                "focus" => MutateOk(() => layout.FocusPane(p.RoomId!, p.PaneId!), p.RoomId, ctx),
                "cycleFocus" => MutateOk(() => layout.CycleFocus(p.RoomId!, p.Dir), p.RoomId, ctx),
                "zoom" => MutateOk(() => layout.SetZoom(p.RoomId!, p.PaneId), p.RoomId, ctx),
                "unzoom" => MutateOk(() => layout.SetZoom(p.RoomId!, null), p.RoomId, ctx),
                "rename" => MutateOk(() => layout.RenameRoom(p.RoomId!, p.Name ?? ""), p.RoomId, ctx),
                _ => ctx.Fail("invalid_params", $"unknown op {p.Op}"),
            });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(ctx.Fail("not_found", "unknown room or pane"));
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(ctx.Fail("invalid_op", "operation not valid for current state"));
        }
    }

    [CoveCommand("cove://commands/session.state")]
    public static Task<ControlResponse> SessionState(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(Cove.Protocol.CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));
        var info = ctx.Panes?.List().FirstOrDefault(x => x.PaneId == p.PaneId);
        if (info is null)
            return Task.FromResult(ctx.Fail("not_found", $"unknown pane {p.PaneId}"));
        return Task.FromResult(ctx.Ok(new SessionStateResult(info.PaneId, info.Command, info.Cols, info.Rows, info.Alive, info.Cwd), Cove.Protocol.CoveJsonContext.Default.SessionStateResult));
    }

    private static ControlResponse MutateOk(Action work, string? roomId, EngineDispatchContext ctx)
    {
        work();
        return ctx.Ok(new LayoutMutateResult(roomId), Cove.Protocol.CoveJsonContext.Default.LayoutMutateResult);
    }

    private static PaneLeaf NewLeaf(string id) => new PaneLeaf
    {
        PaneId = id,
        Subtabs = new[] { new Subtab(id, PaneType.Terminal) },
    };

    private static SplitOrientation Orient(string? s) => (s is "column" or "col" or "vertical") ? SplitOrientation.Column : SplitOrientation.Row;
}
