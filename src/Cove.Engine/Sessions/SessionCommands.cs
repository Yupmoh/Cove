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
        if (ctx.Nooks is not { } nooks)
            return Task.FromResult(ctx.Fail("not_ready", "nook registry not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "nook ref required"));

        orch.Dismiss(p.NookId);
        nooks.Kill(p.NookId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/session.background")]
    public static Task<ControlResponse> Background(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "nook ref required"));

        orch.Background(p.NookId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/session.foreground")]
    public static Task<ControlResponse> Foreground(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "nook ref required"));

        orch.Foreground(p.NookId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/session.stop")]
    public static Task<ControlResponse> Stop(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));
        if (ctx.Nooks is not { } nooks)
            return Task.FromResult(ctx.Fail("not_ready", "nook registry not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "nook ref required"));

        orch.Stop(p.NookId);
        nooks.Kill(p.NookId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/session.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.Sessions is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "session orchestrator not available"));

        var dismissed = orch.ListDismissed().Select(s => new SessionStateDto(s.NookId, s.Adapter, s.SessionId, s.Lifecycle.ToString().ToLowerInvariant(), s.Resumable)).ToList();
        var background = orch.ListBackground().Select(s => new SessionStateDto(s.NookId, s.Adapter, s.SessionId, s.Lifecycle.ToString().ToLowerInvariant(), s.Resumable)).ToList();
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
            if (p.Limit is { } l && l >= 0)
                limit = l;
        }

        var names = adapter is null
            ? manifests.LoadAll().Where(m => m.Methods.ContainsKey("list_recent_sessions")).Select(m => m.Name).ToList()
            : new List<string> { adapter };

        var titleMap = BuildNookTitleMap(ctx.BaysDir);
        var lookups = names.Select(async name =>
        {
            var dir = manifests.ResolveDir(name);
            try
            {
                return (name, sessions: await sessions.ListRecentSessionsAsync(dir, cwd).ConfigureAwait(false));
            }
            catch (Exception)
            {
                return (name, sessions: new List<Cove.Adapters.RecentSession>());
            }
        }).ToList();
        var rows = new List<RecentSessionDto>();
        foreach (var (name, found) in await Task.WhenAll(lookups).ConfigureAwait(false))
        {
            foreach (var s in found)
            {
                var label = titleMap.TryGetValue(s.Id, out var title) ? title : s.Name;
                rows.Add(new RecentSessionDto(name, s.Id, "", s.Cwd ?? cwd, (s.LastActive ?? DateTimeOffset.MinValue).ToString("o"), label));
            }
        }

        rows.Sort((a, b) => string.CompareOrdinal(b.StartedAt, a.StartedAt));

        var deduped = new List<RecentSessionDto>(rows.Count);
        var seenSessions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            if (!seenSessions.Add(r.Adapter + "\0" + r.SessionId))
                continue;
            deduped.Add(r);
        }

        if (limit > 0 && deduped.Count > limit)
            deduped = deduped.GetRange(0, limit);

        return ctx.Ok(new SessionRecentResult(deduped), SessionRecentJsonContext.Default.SessionRecentResult);
    }

    private static Dictionary<string, string> BuildNookTitleMap(string? baysDir)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(baysDir) || !System.IO.Directory.Exists(baysDir))
            return map;
        foreach (var dir in System.IO.Directory.EnumerateDirectories(baysDir))
        {
            var (_, records) = Cove.Engine.Layout.BayPersistence.Load(dir, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            foreach (var d in records.Values)
            {
                if (string.IsNullOrWhiteSpace(d.Title) || string.IsNullOrWhiteSpace(d.SessionId))
                    continue;
                map[d.SessionId!] = d.Title!;
            }
        }
        return map;
    }
}

public sealed record SessionRecentParams(string? Adapter = null, int? Limit = null, string? Cwd = null);
public sealed record RecentSessionDto(string Adapter, string SessionId, string BayId, string Cwd, string StartedAt, string? Label = null);
public sealed record SessionRecentResult(System.Collections.Generic.IReadOnlyList<RecentSessionDto> Sessions);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SessionRecentParams))]
[JsonSerializable(typeof(RecentSessionDto))]
[JsonSerializable(typeof(SessionRecentResult))]
public sealed partial class SessionRecentJsonContext : JsonSerializerContext { }
