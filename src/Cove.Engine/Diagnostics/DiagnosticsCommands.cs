using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Diagnostics;

public static class DiagnosticsCommands
{
    [CoveCommand("cove://commands/diagnostics.status")]
    public static Task<ControlResponse> Status(EngineDispatchContext ctx)
    {
        if (ctx.Diagnostics is not { } hub)
            return Task.FromResult(ctx.Fail("not_ready", "diagnostics hub not available"));

        var cfg = hub.Config;
        var result = new DiagnosticsStatusResult(
            hub.Enabled,
            cfg.WebInspectorOptIn,
            cfg.MaxSnapshots,
            cfg.SnapshotInterval.TotalSeconds,
            hub.GetSnapshots().Count);
        return Task.FromResult(ctx.Ok(result, CoveJsonContext.Default.DiagnosticsStatusResult));
    }

    [CoveCommand("cove://commands/diagnostics.snapshot.take")]
    public static Task<ControlResponse> SnapshotTake(EngineDispatchContext ctx)
    {
        if (ctx.Diagnostics is not { } hub)
            return Task.FromResult(ctx.Fail("not_ready", "diagnostics hub not available"));

        var activePanes = 0;
        var activeWorkspaces = 0;
        var activeAgents = 0;
        if (ctx.Request.Params is JsonElement el && el.Deserialize(CoveJsonContext.Default.DiagnosticsSnapshotTakeParams) is { } p)
        {
            activePanes = p.ActivePanes ?? 0;
            activeWorkspaces = p.ActiveWorkspaces ?? 0;
            activeAgents = p.ActiveAgents ?? 0;
        }

        var snapshot = hub.TakeSnapshot(activePanes, activeWorkspaces, activeAgents);
        return Task.FromResult(ctx.OkJson(hub.ExportSnapshotJson(snapshot)));
    }

    [CoveCommand("cove://commands/diagnostics.snapshot.list")]
    public static Task<ControlResponse> SnapshotList(EngineDispatchContext ctx)
    {
        if (ctx.Diagnostics is not { } hub)
            return Task.FromResult(ctx.Fail("not_ready", "diagnostics hub not available"));

        return Task.FromResult(ctx.OkJson(hub.ExportAllSnapshotsJson()));
    }

    [CoveCommand("cove://commands/diagnostics.export")]
    public static Task<ControlResponse> Export(EngineDispatchContext ctx)
    {
        if (ctx.Diagnostics is not { } hub)
            return Task.FromResult(ctx.Fail("not_ready", "diagnostics hub not available"));

        var payload = hub.ExportAllSnapshotsJson();
        var path = ctx.Request.Params is JsonElement el && el.Deserialize(CoveJsonContext.Default.DiagnosticsExportParams) is { } p
            ? p.Path
            : null;

        if (string.IsNullOrEmpty(path))
            return Task.FromResult(ctx.OkJson(payload));

        try
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(path, payload);
        }
        catch (System.Exception ex)
        {
            return Task.FromResult(ctx.Fail("handler_error", ex.Message));
        }

        return Task.FromResult(ctx.Ok(new DiagnosticsExportResult(path), CoveJsonContext.Default.DiagnosticsExportResult));
    }
}
