using Cove.Engine.Knowledge;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class TimelineStoreTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-tl-" + System.Guid.NewGuid().ToString("N"));

    private static (string dir, TimelineStore store) NewStore()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();
        return (dir, new TimelineStore(dir, NullLogger.Instance));
    }

    [Fact]
    public void Append_ThenList_ReturnsConsistentRow()
    {
        var (_, store) = NewStore();
        var entry = store.Append(new TimelineEntry
        {
            WorkspaceId = "ws1",
            Kind = "note.created",
            Source = "manual",
            Scope = "workspace",
            Summary = "Created a markdown note",
        });

        var list = store.ListByWorkspace("ws1");
        Assert.Single(list);
        Assert.Equal(entry.Id, list[0].Id);
        Assert.Equal("note.created", list[0].Kind);
        Assert.Equal("Created a markdown note", list[0].Summary);
    }

    [Fact]
    public void FtsSearch_ReturnsMatchingEntry()
    {
        var (_, store) = NewStore();
        store.Append(new TimelineEntry
        {
            WorkspaceId = "ws1",
            Kind = "note.created",
            Source = "manual",
            Summary = "Deployed the flibbertigibbet module to production",
        });

        var results = store.Search("ws1", "flibbertigibbet");
        Assert.Single(results);
        Assert.Contains("flibbertigibbet", results[0].Summary);
    }

    [Fact]
    public void BackfillRow_DedupesOnRepeat()
    {
        var (_, store) = NewStore();
        var ts = System.DateTimeOffset.UtcNow;
        var entry = new TimelineEntry
        {
            WorkspaceId = "ws1",
            Kind = "git.commit",
            Source = "git-ingester",
            Summary = "fix: resolve race in scheduler",
            Timestamp = ts,
        };

        store.Append(entry);
        store.Append(entry);

        var list = store.ListByWorkspace("ws1");
        Assert.Single(list);
    }

    [Fact]
    public void ManualEntry_HasNoBackfillKey_AllowsDuplicates()
    {
        var (_, store) = NewStore();
        var entry = new TimelineEntry
        {
            WorkspaceId = "ws1",
            Kind = "note.created",
            Source = "manual",
            Summary = "manual entry",
        };

        store.Append(entry);
        store.Append(entry);

        var list = store.ListByWorkspace("ws1");
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void List_FiltersByWorkspace()
    {
        var (_, store) = NewStore();
        store.Append(new TimelineEntry { WorkspaceId = "ws1", Kind = "a", Source = "manual", Summary = "one" });
        store.Append(new TimelineEntry { WorkspaceId = "ws2", Kind = "b", Source = "manual", Summary = "two" });

        var ws1 = store.ListByWorkspace("ws1");
        var ws2 = store.ListByWorkspace("ws2");
        Assert.Single(ws1);
        Assert.Single(ws2);
        Assert.Equal("a", ws1[0].Kind);
        Assert.Equal("b", ws2[0].Kind);
    }
}
