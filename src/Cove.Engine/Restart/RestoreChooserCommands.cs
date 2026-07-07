using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Engine.Layout;
using Cove.Engine.Pty;
using Cove.Protocol;

namespace Cove.Engine.Restart;

public static class RestoreChooserCommands
{
    [CoveCommand("cove://commands/restore.chooser")]
    public static async Task<ControlResponse> Chooser(EngineDispatchContext ctx)
    {
        if (ctx.Restoration is not { } restoration)
            return ctx.Fail("not_ready", "restoration service unavailable");
        if (ctx.Panes is not { } panes)
            return ctx.Fail("not_ready", "pane registry unavailable");

        var chooser = new RestoreChooserService(restoration);
        var wsDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(restoration.StatePath)!, "workspaces", "default");
        var (savedLayout, sessions) = WorkspacePersistence.Load(wsDir, restoration.Logger);
        var items = new List<RestoreChoiceItem>();
        if (savedLayout is { } layout)
        {
            foreach (var room in layout.Rooms)
                foreach (var leaf in MosaicOps.Leaves(room.LayoutTree))
                    if (sessions.TryGetValue(leaf.PaneId, out var d))
                        items.Add(new RestoreChoiceItem("default", room.Id, d.PaneId, d.Command, WasRunning(panes, d.PaneId), false));
        }

        var result = chooser.Evaluate(items);
        return await Task.FromResult(ctx.Ok(result, RestoreChooserVerbJsonContext.Default.RestoreChooserResult));
    }

    [CoveCommand("cove://commands/restore.confirm")]
    public static async Task<ControlResponse> Confirm(EngineDispatchContext ctx)
    {
        if (ctx.Restoration is not { } restoration)
            return ctx.Fail("not_ready", "restoration service unavailable");
        if (ctx.Panes is not { } panes)
            return ctx.Fail("not_ready", "pane registry unavailable");
        if (ctx.Layout is not { } layout)
            return ctx.Fail("not_ready", "layout service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RestoreChooserVerbJsonContext.Default.RestoreConfirmParams) is not { } p)
            return ctx.Fail("bad_params", "selectedPaneIds required");

        var chooser = new RestoreChooserService(restoration);
        if (p.AutoRestoreOnLaunch)
            chooser.SaveSettings(new RestoreSettings(true));

        var wsDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(restoration.StatePath)!, "workspaces", "default");
        var (savedLayout, sessions) = WorkspacePersistence.Load(wsDir, restoration.Logger);
        var selected = new HashSet<string>(p.SelectedPaneIds ?? [], StringComparer.Ordinal);
        var restored = new List<string>();

        if (savedLayout is { } sl)
        {
            layout.LoadSnapshot(sl);
            foreach (var room in sl.Rooms)
                foreach (var leaf in MosaicOps.Leaves(room.LayoutTree))
                    if (sessions.TryGetValue(leaf.PaneId, out var d) && selected.Contains(d.PaneId))
                    {
                        try
                        {
                            panes.RespawnAs(d.PaneId, d.Command, d.Args, d.Cwd, 80, 24, WorkspacePersistence.LoadScrollback(d.PaneId, wsDir));
                            restored.Add(d.PaneId);
                        }
                        catch { }
                    }
        }

        restoration.MarkLaunching();
        return await Task.FromResult(ctx.Ok(new RestoreConfirmResult(restored), RestoreChooserVerbJsonContext.Default.RestoreConfirmResult));
    }

    [CoveCommand("cove://commands/restore.undo")]
    public static async Task<ControlResponse> Undo(EngineDispatchContext ctx)
    {
        if (ctx.Restoration is not { } restoration)
            return ctx.Fail("not_ready", "restoration service unavailable");
        if (ctx.Panes is not { } panes)
            return ctx.Fail("not_ready", "pane registry unavailable");
        if (ctx.Layout is not { } layout)
            return ctx.Fail("not_ready", "layout service unavailable");

        var wsDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(restoration.StatePath)!, "workspaces", "default");
        var (savedLayout, sessions) = WorkspacePersistence.Load(wsDir, restoration.Logger);
        if (savedLayout is { } sl)
        {
            foreach (var room in sl.Rooms)
                foreach (var leaf in MosaicOps.Leaves(room.LayoutTree))
                    if (sessions.TryGetValue(leaf.PaneId, out var d))
                    {
                        try { panes.RespawnAs(d.PaneId, d.Command, d.Args, d.Cwd, 80, 24, WorkspacePersistence.LoadScrollback(d.PaneId, wsDir)); }
                        catch { }
                    }
            layout.LoadSnapshot(sl);
        }
        restoration.MarkLaunching();
        return await Task.FromResult(ctx.Ok());
    }

    private static bool WasRunning(PaneRegistry panes, string paneId)
        => panes.TryGet(paneId, out _);
}

public sealed record RestoreConfirmParams(IReadOnlyList<string>? SelectedPaneIds, bool AutoRestoreOnLaunch = false);
public sealed record RestoreConfirmResult(IReadOnlyList<string> RestoredPaneIds);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RestoreConfirmParams))]
[JsonSerializable(typeof(RestoreConfirmResult))]
[JsonSerializable(typeof(RestoreChoiceItem))]
[JsonSerializable(typeof(RestoreChooserResult))]
[JsonSerializable(typeof(RestoreSettings))]
public sealed partial class RestoreChooserVerbJsonContext : JsonSerializerContext { }
