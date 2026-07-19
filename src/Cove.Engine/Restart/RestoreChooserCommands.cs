using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Engine.Bays;
using Cove.Engine.Layout;
using Cove.Engine.Pty;
using Cove.Persistence;
using Cove.Protocol;
using Microsoft.Extensions.Logging;
using ZLogger;

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
        if (ctx.Bays is not { } bays)
            return ctx.Fail("not_ready", "bay manager unavailable");
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
            await RestoreBayAsync(bays, entry.Snapshot).ConfigureAwait(false);
            foreach (var shore in entry.Snapshot.Shores)
                foreach (var leaf in MosaicOps.Leaves(shore.LayoutTree))
                    if (entry.Sessions.TryGetValue(leaf.NookId, out var d) && selected.Contains(d.NookId))
                    {
                        try
                        {
                            RespawnPersistent(nooks, d, entry.BayDir, restoration.Logger);
                            restored.Add(d.NookId);
                        }
                        catch (System.Exception ex) { restoration.Logger.SelectedNookRestoreFailed(d.NookId, ex.Message); }
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
        if (ctx.Bays is not { } bays)
            return ctx.Fail("not_ready", "bay manager unavailable");

        var baysRoot = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(restoration.StatePath)!, "bays");
        foreach (var entry in BayStartup.Enumerate(baysRoot, restoration.Logger))
        {
            foreach (var shore in entry.Snapshot.Shores)
                foreach (var leaf in MosaicOps.Leaves(shore.LayoutTree))
                    if (entry.Sessions.TryGetValue(leaf.NookId, out var d))
                    {
                        try { RespawnPersistent(nooks, d, entry.BayDir, restoration.Logger); }
                        catch (System.Exception ex) { restoration.Logger.UndoNookRestoreFailed(d.NookId, ex.Message); }
                    }
            await RestoreBayAsync(bays, entry.Snapshot).ConfigureAwait(false);
        }
        restoration.MarkLaunching();
        return await Task.FromResult(ctx.Ok());
    }

    private static void RespawnPersistent(NookRegistry nooks, NookDescriptor descriptor, string bayDir, ILogger logger)
    {
        var state = BayPersistence.LoadTerminalRestoreState(descriptor.NookId, bayDir, logger);

        if (state is not null)
            nooks.RespawnAs(descriptor.NookId, descriptor.Command, descriptor.Args, descriptor.Cwd, descriptor.Cols, descriptor.Rows, state);
        else
            nooks.RespawnAs(descriptor.NookId, descriptor.Command, descriptor.Args, descriptor.Cwd, descriptor.Cols, descriptor.Rows, BayPersistence.LoadScrollback(descriptor.NookId, bayDir));
    }

    private static bool WasRunning(NookRegistry nooks, string nookId)
        => nooks.Contains(nookId);

    private static Task<BayModel> RestoreBayAsync(BayManager bays, BaySnapshot snapshot)
    {
        var name = BayStartup.DisplayName(snapshot, Environment.CurrentDirectory);
        var projectDir = string.IsNullOrWhiteSpace(snapshot.ProjectDir)
            ? Environment.CurrentDirectory
            : snapshot.ProjectDir;
        var icon = string.IsNullOrWhiteSpace(snapshot.IconKind)
            ? null
            : new BayIcon(snapshot.IconKind, snapshot.IconValue ?? "");
        return bays.RestoreBayAsync(snapshot, name, projectDir, icon: icon);
    }
}

internal static partial class RestoreChooserLog
{
    [ZLoggerMessage(LogLevel.Warning, "selected nook restore failed nook={nookId} error={error}")]
    public static partial void SelectedNookRestoreFailed(this ILogger logger, string nookId, string error);

    [ZLoggerMessage(LogLevel.Warning, "undo nook restore failed nook={nookId} error={error}")]
    public static partial void UndoNookRestoreFailed(this ILogger logger, string nookId, string error);
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
