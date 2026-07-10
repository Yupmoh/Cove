using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine;
using Cove.Engine.Sessions;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SessionRecentCommandTests
{
    private static string NewDir()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-recent-cmd-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    private static Task<ControlResponse?> Route(RecentSessionStore store, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", "cove://commands/session.recent", prm), recentSessions: store);

    private static JsonElement El<T>(T v, JsonTypeInfo<T> ti) => JsonSerializer.SerializeToElement(v, ti);

    [Fact]
    public async Task Recent_ReturnsNewestFirst()
    {
        var store = new RecentSessionStore(NewDir(), NullLogger.Instance);
        var b = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
        store.RecordStart("claude", "s1", "ws-1", "/a", b);
        store.RecordStart("claude", "s2", "ws-1", "/b", b.AddMinutes(5));

        var resp = await Route(store, El(new SessionRecentParams(null, null), SessionRecentJsonContext.Default.SessionRecentParams));

        Assert.True(resp!.Ok);
        var sessions = resp.Data!.Value.GetProperty("sessions");
        Assert.Equal("s2", sessions[0].GetProperty("sessionId").GetString());
        Assert.Equal("s1", sessions[1].GetProperty("sessionId").GetString());
    }

    [Fact]
    public async Task Recent_HonoursAdapterAndLimit()
    {
        var store = new RecentSessionStore(NewDir(), NullLogger.Instance);
        var b = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
        store.RecordStart("claude", "c1", "ws-1", "/a", b);
        store.RecordStart("claude", "c2", "ws-1", "/b", b.AddMinutes(1));
        store.RecordStart("codex", "x1", "ws-1", "/c", b.AddMinutes(2));

        var resp = await Route(store, El(new SessionRecentParams("claude", 1), SessionRecentJsonContext.Default.SessionRecentParams));

        Assert.True(resp!.Ok);
        var sessions = resp.Data!.Value.GetProperty("sessions");
        Assert.Equal(1, sessions.GetArrayLength());
        Assert.Equal("c2", sessions[0].GetProperty("sessionId").GetString());
    }

    [Fact]
    public async Task Recent_NoStore_IsNotReady()
    {
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("1", "cove://commands/session.recent", null));
        Assert.False(resp!.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }
}
