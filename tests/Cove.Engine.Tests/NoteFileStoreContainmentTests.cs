using Cove.Engine.Knowledge;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NoteFileStoreContainmentTests
{
    private static (string dataDir, NoteFileStore store) NewStore()
    {
        var dataDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-note-containment-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dataDir);
        var kernel = new KnowledgePersistenceKernel(dataDir, NullLogger.Instance);
        kernel.EnsureAllSchemas();
        return (dataDir, new NoteFileStore(dataDir, NullLogger.Instance));
    }

    [Fact]
    public void Create_TraversalIdentifiersThrowWithoutWritingOutsideNotesRoot()
    {
        var (dataDir, store) = NewStore();
        var outsideNoteDir = System.IO.Path.Combine(dataDir, "outside-note");

        Assert.Throws<System.ArgumentException>(() => store.Create(new Note
        {
            Id = "outside-note",
            BayId = "..",
            Source = "test",
            Title = "escaped",
            Content = "escaped",
            Kind = "markdown",
        }));
        Assert.Throws<System.ArgumentException>(() => store.Create(new Note
        {
            Id = "../../outside-note",
            BayId = "bay",
            Source = "test",
            Title = "escaped",
            Content = "escaped",
            Kind = "markdown",
        }));

        Assert.False(System.IO.Directory.Exists(outsideNoteDir));
    }

    [Fact]
    public void GetAndUpdate_TraversalIdentifiersDoNotReadOrWriteOutsideNotesRoot()
    {
        var (dataDir, store) = NewStore();
        var outsideNoteDir = System.IO.Path.Combine(dataDir, "outside-note");
        System.IO.Directory.CreateDirectory(outsideNoteDir);
        var metaPath = System.IO.Path.Combine(outsideNoteDir, "meta.json");
        var bodyPath = System.IO.Path.Combine(outsideNoteDir, "note.md");
        const string meta = """{"id":"outside-note","title":"sentinel","bayId":"..","source":"test","kind":"markdown","createdAt":"2026-01-01T00:00:00+00:00","updatedAt":"2026-01-01T00:00:00+00:00"}""";
        System.IO.File.WriteAllText(metaPath, meta);
        System.IO.File.WriteAllText(bodyPath, "sentinel body");

        Assert.Null(store.Get("..", "outside-note"));
        store.Update("..", "outside-note", note => note with { Title = "changed", Content = "changed" });
        Assert.Null(store.Get("bay", "../../outside-note"));
        store.Update("bay", "../../outside-note", note => note with { Title = "changed", Content = "changed" });

        Assert.Equal(meta, System.IO.File.ReadAllText(metaPath));
        Assert.Equal("sentinel body", System.IO.File.ReadAllText(bodyPath));
    }

    [Fact]
    public void Delete_TraversalIdentifiersPreserveOutsideSentinel()
    {
        var (dataDir, store) = NewStore();
        var outsideNoteDir = System.IO.Path.Combine(dataDir, "outside-note");
        System.IO.Directory.CreateDirectory(outsideNoteDir);
        var sentinelPath = System.IO.Path.Combine(outsideNoteDir, "sentinel.txt");
        System.IO.File.WriteAllText(sentinelPath, "keep");

        store.Delete("..", "outside-note");
        store.Delete("bay", "../../outside-note");

        Assert.True(System.IO.File.Exists(sentinelPath));
        Assert.Equal("keep", System.IO.File.ReadAllText(sentinelPath));
    }

    [Fact]
    public void SaveMedia_TraversalIdentifiersAndFileNameThrowWithoutWritingOutsideNotesRoot()
    {
        var (dataDir, store) = NewStore();
        var outsideNoteDir = System.IO.Path.Combine(dataDir, "outside-note");

        Assert.Throws<System.ArgumentException>(() => store.SaveMedia("..", "outside-note", "image.png", [1, 2, 3]));
        Assert.Throws<System.ArgumentException>(() => store.SaveMedia("bay", "../../outside-note", "image.png", [1, 2, 3]));
        Assert.Throws<System.ArgumentException>(() => store.SaveMedia("bay", "note", "../image.png", [1, 2, 3]));

        Assert.False(System.IO.Directory.Exists(outsideNoteDir));
        Assert.False(System.IO.Directory.Exists(System.IO.Path.Combine(dataDir, "notes", "bay")));
    }

    [Fact]
    public void ViewportStateAndList_TraversalIdentifiersReturnSafeFailuresWithoutOutsideIo()
    {
        var (dataDir, store) = NewStore();
        var outsideNoteDir = System.IO.Path.Combine(dataDir, "outside-note");
        System.IO.Directory.CreateDirectory(outsideNoteDir);
        var viewportPath = System.IO.Path.Combine(outsideNoteDir, "viewport.json");
        var statePath = System.IO.Path.Combine(outsideNoteDir, "state.json");
        System.IO.File.WriteAllText(viewportPath, "sentinel viewport");
        System.IO.File.WriteAllText(statePath, "sentinel state");

        store.SaveViewport("..", "outside-note", "changed");
        store.SaveState("..", "outside-note", "changed");

        Assert.Null(store.LoadViewport("..", "outside-note"));
        Assert.Null(store.LoadState("..", "outside-note"));
        Assert.Empty(store.ListByBay(".."));
        Assert.Equal("sentinel viewport", System.IO.File.ReadAllText(viewportPath));
        Assert.Equal("sentinel state", System.IO.File.ReadAllText(statePath));
    }
}
