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
    public static Task<ControlResponse> Recent(EngineDispatchContext ctx)
    {
        if (ctx.RecentSessions is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "recent session store not available"));

        string? adapter = null;
        var limit = 20;
        if (ctx.Request.Params is JsonElement el
            && el.Deserialize(SessionRecentJsonContext.Default.SessionRecentParams) is { } p)
        {
            adapter = string.IsNullOrWhiteSpace(p.Adapter) ? null : p.Adapter;
            if (p.Limit is { } l && l > 0)
                limit = l;
        }

        var rows = store.Recent(adapter, limit)
            .Select(r => new RecentSessionDto(r.Adapter, r.SessionId, r.WorkspaceId, r.Cwd, r.StartedAt.ToString("o")))
            .ToList();
        return Task.FromResult(ctx.Ok(new SessionRecentResult(rows), SessionRecentJsonContext.Default.SessionRecentResult));
    }
}

public sealed record SessionRecentParams(string? Adapter = null, int? Limit = null);
public sealed record RecentSessionDto(string Adapter, string SessionId, string WorkspaceId, string Cwd, string StartedAt);
public sealed record SessionRecentResult(System.Collections.Generic.IReadOnlyList<RecentSessionDto> Sessions);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SessionRecentParams))]
[JsonSerializable(typeof(RecentSessionDto))]
[JsonSerializable(typeof(SessionRecentResult))]
public sealed partial class SessionRecentJsonContext : JsonSerializerContext { }
