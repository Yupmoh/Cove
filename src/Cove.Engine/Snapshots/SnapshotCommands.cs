using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Snapshots;

public static class SnapshotCommands
{
    [CoveCommand("cove://commands/snapshot.take")]
    public static async Task<ControlResponse> Take(EngineDispatchContext ctx)
    {
        if (ctx.Snapshots is not { } svc)
            return ctx.Fail("not_ready", "snapshot service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(SnapshotVerbJsonContext.Default.SnapshotTakeParams) is not { } p)
            return ctx.Fail("bad_params", "content and trigger are required");

        var content = p.Content ?? new Dictionary<string, string>();
        var trigger = ParseTrigger(p.Trigger ?? "manual");
        var snap = await svc.TakeAsync(content, trigger).ConfigureAwait(false);
        return snap is null
            ? ctx.Fail("snapshot_skipped", "dedup: no state change")
            : ctx.Ok(new SnapshotResult(snap.Id, snap.Hash, snap.Trigger.ToString(), snap.TakenAtUtc), SnapshotVerbJsonContext.Default.SnapshotResult);
    }

    [CoveCommand("cove://commands/snapshot.list")]
    public static async Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.Snapshots is not { } svc)
            return ctx.Fail("not_ready", "snapshot service unavailable");
        var snapshots = await svc.ListRetainedAsync().ConfigureAwait(false);
        var result = snapshots.Select(s => new SnapshotListItem(s.Id, s.Hash, s.Trigger.ToString(), s.TakenAtUtc, s.Pinned)).ToList();
        return ctx.Ok(new SnapshotListResult(result), SnapshotVerbJsonContext.Default.SnapshotListResult);
    }

    [CoveCommand("cove://commands/snapshot.restore")]
    public static async Task<ControlResponse> Restore(EngineDispatchContext ctx)
    {
        if (ctx.Snapshots is not { } svc)
            return ctx.Fail("not_ready", "snapshot service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(SnapshotVerbJsonContext.Default.SnapshotIdParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Id))
            return ctx.Fail("bad_params", "id is required");

        var content = await svc.RestoreAsync(p.Id).ConfigureAwait(false);
        return content is null
            ? ctx.Fail("not_found", "snapshot not found")
            : ctx.Ok(new SnapshotRestoreResult(content), SnapshotVerbJsonContext.Default.SnapshotRestoreResult);
    }

    [CoveCommand("cove://commands/snapshot.pin")]
    public static async Task<ControlResponse> Pin(EngineDispatchContext ctx)
    {
        if (ctx.Snapshots is not { } svc)
            return ctx.Fail("not_ready", "snapshot service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(SnapshotVerbJsonContext.Default.SnapshotIdParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Id))
            return ctx.Fail("bad_params", "id is required");

        await svc.PinAsync(p.Id).ConfigureAwait(false);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/snapshot.prune")]
    public static async Task<ControlResponse> Prune(EngineDispatchContext ctx)
    {
        if (ctx.Snapshots is not { } svc)
            return ctx.Fail("not_ready", "snapshot service unavailable");

        await svc.PruneAsync().ConfigureAwait(false);
        return ctx.Ok();
    }
    [CoveCommand("cove://commands/snapshot.inspect")]
    public static async Task<ControlResponse> Inspect(EngineDispatchContext ctx)
    {
        if (ctx.Snapshots is not { } svc)
            return ctx.Fail("not_ready", "snapshot service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(SnapshotVerbJsonContext.Default.SnapshotIdParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Id))
            return ctx.Fail("bad_params", "id is required");

        var diffs = await svc.InspectAsync(p.Id).ConfigureAwait(false);
        if (diffs is null)
            return ctx.Fail("not_found", "snapshot not found");

        var dtos = diffs.Select(d => new SnapshotDiffItem(d.Key, d.OldValue, d.NewValue, d.ChangeType)).ToList();
        return ctx.Ok(new SnapshotInspectResult(dtos), SnapshotVerbJsonContext.Default.SnapshotInspectResult);
    }

    private static SnapshotTrigger ParseTrigger(string trigger) => trigger.ToLowerInvariant() switch
    {
        "interval" => SnapshotTrigger.Interval,
        "shutdown" => SnapshotTrigger.Shutdown,
        "pre-update" => SnapshotTrigger.PreUpdate,
        "pre-restore" => SnapshotTrigger.PreRestore,
        "manual" => SnapshotTrigger.Manual,
        "event" => SnapshotTrigger.Event,
        _ => SnapshotTrigger.Manual,
    };
}

public sealed record SnapshotTakeParams(IReadOnlyDictionary<string, string>? Content, string? Trigger);
public sealed record SnapshotIdParams(string Id);
public sealed record SnapshotResult(string Id, string Hash, string Trigger, DateTimeOffset TakenAtUtc);
public sealed record SnapshotListItem(string Id, string Hash, string Trigger, DateTimeOffset TakenAtUtc, bool Pinned);
public sealed record SnapshotListResult(IReadOnlyList<SnapshotListItem> Snapshots);
public sealed record SnapshotRestoreResult(IReadOnlyDictionary<string, string> Content);
public sealed record SnapshotDiffItem(string Key, string? OldValue, string? NewValue, string ChangeType);
public sealed record SnapshotInspectResult(IReadOnlyList<SnapshotDiffItem> Diffs);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SnapshotTakeParams))]
[JsonSerializable(typeof(SnapshotIdParams))]
[JsonSerializable(typeof(SnapshotResult))]
[JsonSerializable(typeof(SnapshotListItem))]
[JsonSerializable(typeof(SnapshotListResult))]
[JsonSerializable(typeof(SnapshotRestoreResult))]
[JsonSerializable(typeof(SnapshotDiffItem))]
[JsonSerializable(typeof(SnapshotInspectResult))]
public sealed partial class SnapshotVerbJsonContext : JsonSerializerContext { }
