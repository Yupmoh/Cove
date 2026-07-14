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
        if (ctx.Nooks is not { } nooks)
            return ctx.Fail("not_ready", "nook registry unavailable");

        var chooser = new RestoreChooserService(restoration);
        var baysRoot = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(restoration.StatePath)!, "bays");
        var items = new List<RestoreChoiceItem>();
        foreach (var entry in BayStartup.Enumerate(baysRoot, restoration.Logger))
            foreach (var shore in entry.Snapshot.Shores)
                foreach (var leaf in MosaicOps.Leaves(shore.LayoutTree))
                    if (entry.Sessions.TryGetValue(leaf.NookId, out var d))
                        items.Add(new RestoreChoiceItem(entry.Snapshot.Id, shore.Id, d.NookId, d.Command, WasRunning(nooks, d.NookId), false));

        var result = chooser.Evaluate(items);
        return await Task.FromResult(ctx.Ok(result, RestoreChooserVerbJsonContext.Default.RestoreChooserResult));
    }

    [CoveCommand("cove://commands/restore.confirm")]
    public static async Task<ControlResponse> Confirm(EngineDispatchContext ctx)
    {
        if (ctx.Restoration is not { } restoration)
            return ctx.Fail("not_ready", "restoration service unavailable");
        if (ctx.Nooks is not { } nooks)
            return ctx.Fail("not_ready", "nook registry unavailable");
        if (ctx.Layout is not { } layout)
            return ctx.Fail("not_ready", "layout service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RestoreChooserVerbJsonContext.Default.RestoreConfirmParams) is not { } p)
            return ctx.Fail("bad_params", "selectedNookIds required");

        var chooser = new RestoreChooserService(restoration);
        if (p.AutoRestoreOnLaunch)
            chooser.SaveSettings(new RestoreSettings(true));

        var baysRoot = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(restoration.StatePath)!, "bays");
        var selected = new HashSet<string>(p.SelectedNookIds ?? [], StringComparer.Ordinal);
        var restored = new List<string>();

        foreach (var entry in BayStartup.Enumerate(baysRoot, restoration.Logger))
        {
            layout.LoadSnapshot(entry.Snapshot);
            foreach (var shore in entry.Snapshot.Shores)
                foreach (var leaf in MosaicOps.Leaves(shore.LayoutTree))
                    if (entry.Sessions.TryGetValue(leaf.NookId, out var d) && selected.Contains(d.NookId))
                    {
                        try
                        {
                            nooks.RespawnAs(d.NookId, d.Command, d.Args, d.Cwd, d.Cols, d.Rows, BayPersistence.LoadScrollback(d.NookId, entry.BayDir));
                            restored.Add(d.NookId);
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
        if (ctx.Nooks is not { } nooks)
            return ctx.Fail("not_ready", "nook registry unavailable");
        if (ctx.Layout is not { } layout)
            return ctx.Fail("not_ready", "layout service unavailable");

        var baysRoot = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(restoration.StatePath)!, "bays");
        foreach (var entry in BayStartup.Enumerate(baysRoot, restoration.Logger))
        {
            foreach (var shore in entry.Snapshot.Shores)
                foreach (var leaf in MosaicOps.Leaves(shore.LayoutTree))
                    if (entry.Sessions.TryGetValue(leaf.NookId, out var d))
                    {
                        try { nooks.RespawnAs(d.NookId, d.Command, d.Args, d.Cwd, d.Cols, d.Rows, BayPersistence.LoadScrollback(d.NookId, entry.BayDir)); }
                        catch { }
                    }
            layout.LoadSnapshot(entry.Snapshot);
        }
        restoration.MarkLaunching();
        return await Task.FromResult(ctx.Ok());
    }

    private static bool WasRunning(NookRegistry nooks, string nookId)
        => nooks.TryGet(nookId, out _);
}

public sealed record RestoreConfirmParams(IReadOnlyList<string>? SelectedNookIds, bool AutoRestoreOnLaunch = false);
public sealed record RestoreConfirmResult(IReadOnlyList<string> RestoredNookIds);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RestoreConfirmParams))]
[JsonSerializable(typeof(RestoreConfirmResult))]
[JsonSerializable(typeof(RestoreChoiceItem))]
[JsonSerializable(typeof(RestoreChooserResult))]
[JsonSerializable(typeof(RestoreSettings))]
public sealed partial class RestoreChooserVerbJsonContext : JsonSerializerContext { }
