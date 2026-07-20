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
        var requestedBayId = ctx.Request.Params is JsonElement el
            ? el.Deserialize(Cove.Protocol.CoveJsonContext.Default.LayoutGetParams)?.BayId
            : null;
        requestedBayId ??= CallerBayId(ctx, layout);
        var snapshot = string.IsNullOrEmpty(requestedBayId)
            ? ActiveSnapshot(ctx, layout)
            : SnapshotFor(ctx, layout, requestedBayId);
        return Task.FromResult(ctx.Ok(snapshot, Cove.Persistence.CoveJsonContext.Default.BaySnapshot));
    }

    [CoveCommand("cove://commands/layout.snapshot")]
    public static Task<ControlResponse> LayoutSnapshot(EngineDispatchContext ctx)
    {
        if (ctx.Layout is not { } layout)
            return Task.FromResult(ctx.Fail("not_ready", "layout service unavailable"));
        var callerBayId = CallerBayId(ctx, layout);
        var snapshot = string.IsNullOrEmpty(callerBayId)
            ? ActiveSnapshot(ctx, layout)
            : SnapshotFor(ctx, layout, callerBayId);
        return Task.FromResult(ctx.Ok(snapshot, Cove.Persistence.CoveJsonContext.Default.BaySnapshot));
    }

    private static string? CallerBayId(
        EngineDispatchContext ctx,
        Cove.Engine.Layout.LayoutService layout) =>
        string.IsNullOrEmpty(ctx.Request.CallerNookId)
            ? null
            : layout.ResolveNookLocation(
                ctx.Request.CallerNookId).BayId;

    private static Cove.Persistence.BaySnapshot SnapshotFor(EngineDispatchContext ctx, Cove.Engine.Layout.LayoutService layout, string wsId)
    {
        var name = wsId;
        var projectDir = Environment.CurrentDirectory;
        if (ctx.Bays?.Get(wsId) is { } actor)
        {
            name = actor.State.Name;
            projectDir = actor.State.ProjectDir;
        }
        return layout.ToSnapshot(wsId, name, projectDir);
    }

    private static Cove.Persistence.BaySnapshot ActiveSnapshot(EngineDispatchContext ctx, Cove.Engine.Layout.LayoutService layout)
    {
        var wsId = layout.ActiveBayId;
        var name = wsId;
        var projectDir = Environment.CurrentDirectory;
        if (ctx.Bays?.Get(wsId) is { } actor)
        {
            name = actor.State.Name;
            projectDir = actor.State.ProjectDir;
        }
        return layout.ToSnapshot(wsId, name, projectDir);
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
                "createShore" => ctx.Ok(new LayoutMutateResult(layout.CreateShore(p.Name ?? "main", NewLeaf(p.NewNookId!, p.NookType))), Cove.Protocol.CoveJsonContext.Default.LayoutMutateResult),
                "split" => MutateOk(() => layout.SplitNook(p.ShoreId!, p.TargetNookId!, Orient(p.Orientation), NewLeaf(p.NewNookId!, p.NookType)), p.ShoreId, ctx),
                "replace" => MutateOk(() => layout.ReplaceNook(p.ShoreId!, p.TargetNookId!, NewLeaf(p.NewNookId!, p.NookType)), p.ShoreId, ctx),
                "close" => MutateOk(() => layout.CloseNook(p.ShoreId!, p.NookId!), p.ShoreId, ctx),
                "closeShore" => CloseShoreOk(() => layout.CloseShore(p.ShoreId!), p.ShoreId, ctx),
                "addSubtab" => MutateOk(() => layout.AddSubtab(p.ShoreId!, p.NookId!, p.NewNookId!), p.ShoreId, ctx),
                "activateSubtab" => MutateOk(() => layout.ActivateSubtab(p.ShoreId!, p.NookId!, p.Dir), p.ShoreId, ctx),
                "promoteSubtab" => MutateOk(() => layout.PromoteSubtab(p.ShoreId!, p.NookId!, p.Dir, p.NewNookId!), p.ShoreId, ctx),
                "centerDrop" => MutateOk(() => layout.CenterDrop(p.ShoreId!, p.TargetNookId!, p.Dir, p.NookId!), p.ShoreId, ctx),
                "moveNook" => MutateOk(() => layout.MoveNook(p.ShoreId!, p.NookId!, p.TargetNookId!, Orient(p.Orientation), p.Dir), p.ShoreId, ctx),
                "moveNookToShore" => MutateOk(() => layout.MoveNookToShore(p.NookId!, p.ShoreId!), p.ShoreId, ctx),
                "focus" => MutateOk(() => layout.FocusNook(p.ShoreId!, p.NookId!), p.ShoreId, ctx),
                "cycleFocus" => MutateOk(() => layout.CycleFocus(p.ShoreId!, p.Dir), p.ShoreId, ctx),
                "zoom" => MutateOk(() => layout.SetZoom(p.ShoreId!, p.NookId), p.ShoreId, ctx),
                "unzoom" => MutateOk(() => layout.SetZoom(p.ShoreId!, null), p.ShoreId, ctx),
                "rename" => MutateOk(() => layout.RenameShore(p.ShoreId!, p.Name ?? ""), p.ShoreId, ctx),
                "reorder" => ReorderOk(() => layout.ReorderShores(p.ShoreIds ?? System.Array.Empty<string>()), ctx),
                _ => ctx.Fail("invalid_params", $"unknown op {p.Op}"),
            });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(ctx.Fail("not_found", "unknown shore or nook"));
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(ctx.Fail("invalid_op", "operation not valid for current state"));
        }
    }

    [CoveCommand("cove://commands/session.state")]
    public static Task<ControlResponse> SessionState(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(Cove.Protocol.CoveJsonContext.Default.NookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "nook ref required"));
        var info = ctx.Nooks?.List().FirstOrDefault(x => x.NookId == p.NookId);
        if (info is null)
            return Task.FromResult(ctx.Fail("not_found", $"unknown nook {p.NookId}"));
        return Task.FromResult(ctx.Ok(new SessionStateResult(info.NookId, info.Command, info.Cols, info.Rows, info.Alive, info.Cwd), Cove.Protocol.CoveJsonContext.Default.SessionStateResult));
    }

    private static ControlResponse MutateOk(Action work, string? shoreId, EngineDispatchContext ctx)
    {
        work();
        return ctx.Ok(new LayoutMutateResult(shoreId), Cove.Protocol.CoveJsonContext.Default.LayoutMutateResult);
    }

    private static ControlResponse CloseShoreOk(Action work, string? shoreId, EngineDispatchContext ctx)
    {
        work();
        return ctx.Ok(new LayoutMutateResult(shoreId), Cove.Protocol.CoveJsonContext.Default.LayoutMutateResult);
    }

    private static ControlResponse ReorderOk(Action work, EngineDispatchContext ctx)
    {
        work();
        return ctx.Ok(new LayoutMutateResult(null), Cove.Protocol.CoveJsonContext.Default.LayoutMutateResult);
    }

    private static NookLeaf NewLeaf(string id, string? nookType = null) => new NookLeaf
    {
        NookId = id,
        Subtabs = new[] { new Subtab(id, ParseNookType(nookType)) },
    };

    private static NookType ParseNookType(string? s) => s switch
    {
        "terminal" or null or "" => NookType.Terminal,
        "empty" => NookType.Empty,
        "editor" => NookType.Editor,
        "markdown" => NookType.Markdown,
        "search" => NookType.Search,
        "sourceControl" or "git" => NookType.SourceControl,
        "browser" => NookType.Browser,
        "image" => NookType.Image,
        "diff" => NookType.Diff,
        "pdf" => NookType.Pdf,
        "video" => NookType.Video,
        "tasks-list" => NookType.Tasks,
        "notepad" => NookType.Notepad,
        _ => NookType.Terminal,
    };

    private static SplitOrientation Orient(string? s) => (s is "column" or "col" or "vertical") ? SplitOrientation.Column : SplitOrientation.Row;
}
