using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Sessions;

public static class SessionCommands
{
    [CoveCommand("cove://commands/session.dismiss")]
    public static Task<ControlResponse> Dismiss(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));
        if (ctx.Panes is not { } panes)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));

        orch.Dismiss(p.PaneId);
        panes.Kill(p.PaneId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/session.background")]
    public static Task<ControlResponse> Background(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));

        orch.Background(p.PaneId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/session.foreground")]
    public static Task<ControlResponse> Foreground(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));

        orch.Foreground(p.PaneId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/session.stop")]
    public static Task<ControlResponse> Stop(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));
        if (ctx.Panes is not { } panes)
            return Task.FromResult(ctx.Fail("not_ready", "pane registry not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.PaneRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "pane ref required"));

        orch.Stop(p.PaneId);
        panes.Kill(p.PaneId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/session.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));

        var dismissed = orch.ListDismissed().Select(s => new SessionStateDto(s.PaneId, s.Adapter, s.SessionId, s.Lifecycle.ToString().ToLowerInvariant(), s.Resumable)).ToList();
        var background = orch.ListBackground().Select(s => new SessionStateDto(s.PaneId, s.Adapter, s.SessionId, s.Lifecycle.ToString().ToLowerInvariant(), s.Resumable)).ToList();
        var all = dismissed.Concat(background).ToList();
        return Task.FromResult(ctx.Ok(new SessionListResult(all), CoveJsonContext.Default.SessionListResult));
    }

    [CoveCommand("cove://commands/session.recent")]
    public static async Task<ControlResponse> Recent(EngineDispatchContext ctx)
    {
        if (ctx.SessionService is not { } sessions)
            return ctx.Fail("not_ready", "session service not available");
        if (ctx.ManifestStore is not { } manifests)
            return ctx.Fail("not_ready", "adapter manifest store not available");

        string? adapter = null;
        var cwd = "";
        var limit = 20;
        if (ctx.Request.Params is JsonElement el
            && el.Deserialize(SessionRecentJsonContext.Default.SessionRecentParams) is { } p)
        {
            adapter = string.IsNullOrWhiteSpace(p.Adapter) ? null : p.Adapter;
            cwd = p.Cwd ?? "";
            if (p.Limit is { } l && l > 0)
                limit = l;
        }

        var names = adapter is null
            ? manifests.LoadAll().Select(m => m.Name).ToList()
            : new List<string> { adapter };

        var rows = new List<RecentSessionDto>();
        foreach (var name in names)
        {
            var dir = manifests.ResolveDir(name);
            List<Cove.Adapters.RecentSession> found;
            try
            {
                found = await sessions.ListRecentSessionsAsync(dir, cwd).ConfigureAwait(false);
            }
            catch (Exception)
            {
                continue;
            }
            foreach (var s in found)
                rows.Add(new RecentSessionDto(name, s.Id, "", s.Cwd ?? cwd, (s.LastActive ?? DateTimeOffset.MinValue).ToString("o"), s.Name));
        }

        rows.Sort((a, b) => string.CompareOrdinal(b.StartedAt, a.StartedAt));
        if (limit > 0 && rows.Count > limit)
            rows = rows.GetRange(0, limit);

        return ctx.Ok(new SessionRecentResult(rows), SessionRecentJsonContext.Default.SessionRecentResult);
    }
}

public sealed record SessionRecentParams(string? Adapter = null, int? Limit = null, string? Cwd = null);
public sealed record RecentSessionDto(string Adapter, string SessionId, string WorkspaceId, string Cwd, string StartedAt, string? Label = null);
public sealed record SessionRecentResult(System.Collections.Generic.IReadOnlyList<RecentSessionDto> Sessions);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SessionRecentParams))]
[JsonSerializable(typeof(RecentSessionDto))]
[JsonSerializable(typeof(SessionRecentResult))]
public sealed partial class SessionRecentJsonContext : JsonSerializerContext { }
