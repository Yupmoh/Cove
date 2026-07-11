using System;
using System.Linq;
using Cove.Engine.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class RecentSessionStoreTests
{
    private static string NewDir()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-recent-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    private static readonly DateTimeOffset Base = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecordStart_RoundTrips()
    {
        var store = new RecentSessionStore(NewDir(), NullLogger.Instance);
        store.RecordStart("claude", "s1", "ws-1", "/home/moh/proj", Base);

        var rows = store.Recent(null, 10);
        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal("claude", row.Adapter);
        Assert.Equal("s1", row.SessionId);
        Assert.Equal("ws-1", row.BayId);
        Assert.Equal("/home/moh/proj", row.Cwd);
        Assert.Equal(Base, row.StartedAt);
    }

    [Fact]
    public void Recent_IsNewestFirst()
    {
        var store = new RecentSessionStore(NewDir(), NullLogger.Instance);
        store.RecordStart("claude", "s1", "ws-1", "/a", Base);
        store.RecordStart("claude", "s2", "ws-1", "/b", Base.AddMinutes(5));
        store.RecordStart("claude", "s3", "ws-1", "/c", Base.AddMinutes(1));

        var rows = store.Recent(null, 10);
        Assert.Equal(new[] { "s2", "s3", "s1" }, rows.Select(r => r.SessionId).ToArray());
    }

    [Fact]
    public void Recent_HonoursLimit()
    {
        var store = new RecentSessionStore(NewDir(), NullLogger.Instance);
        for (var i = 0; i < 5; i++)
            store.RecordStart("claude", $"s{i}", "ws-1", "/a", Base.AddMinutes(i));

        Assert.Equal(3, store.Recent(null, 3).Count);
    }

    [Fact]
    public void Recent_FiltersByAdapter()
    {
        var store = new RecentSessionStore(NewDir(), NullLogger.Instance);
        store.RecordStart("claude", "c1", "ws-1", "/a", Base);
        store.RecordStart("codex", "x1", "ws-1", "/b", Base.AddMinutes(1));

        var rows = store.Recent("codex", 10);
        Assert.Single(rows);
        Assert.Equal("x1", rows[0].SessionId);
    }

    [Fact]
    public void PurgeAdapter_RemovesOnlyThatAdapter()
    {
        var store = new RecentSessionStore(NewDir(), NullLogger.Instance);
        store.RecordStart("claude", "c1", "ws-1", "/a", Base);
        store.RecordStart("claude", "c2", "ws-1", "/a", Base.AddMinutes(1));
        store.RecordStart("codex", "x1", "ws-1", "/b", Base.AddMinutes(2));

        var purged = store.PurgeAdapter("claude");

        Assert.Equal(2, purged);
        var rows = store.Recent(null, 10);
        Assert.Single(rows);
        Assert.Equal("x1", rows[0].SessionId);
    }

    [Fact]
    public void PurgeAdapter_EmptyName_NoOp()
    {
        var store = new RecentSessionStore(NewDir(), NullLogger.Instance);
        store.RecordStart("claude", "c1", "ws-1", "/a", Base);

        Assert.Equal(0, store.PurgeAdapter(""));
        Assert.Single(store.Recent(null, 10));
    }
}
