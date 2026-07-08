using Cove.Engine.Knowledge;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NoteFileStoreTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-notes-" + System.Guid.NewGuid().ToString("N"));

    private static (string dir, NoteFileStore store) NewStore()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();
        return (dir, new NoteFileStore(dir, NullLogger.Instance));
    }

    [Fact]
    public void Create_PersistsAsFilesWithMetaAndBody()
    {
        var (dir, store) = NewStore();
        var note = store.Create(new Note { Title = "My Note", WorkspaceId = "ws1", Content = "Hello world", Source = "manual", Kind = "markdown" });

        var noteDir = System.IO.Path.Combine(dir, "notes", "ws1", note.Id);
        Assert.True(System.IO.File.Exists(System.IO.Path.Combine(noteDir, "meta.json")));
        Assert.True(System.IO.File.Exists(System.IO.Path.Combine(noteDir, "note.md")));
        var body = System.IO.File.ReadAllText(System.IO.Path.Combine(noteDir, "note.md"));
        Assert.Equal("Hello world", body);
    }

    [Fact]
    public void Get_ReadsFromFiles()
    {
        var (_, store) = NewStore();
        var created = store.Create(new Note { Title = "Test", WorkspaceId = "ws1", Content = "Body content", Source = "manual", Kind = "markdown" });

        var retrieved = store.Get("ws1", created.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Test", retrieved!.Title);
        Assert.Equal("Body content", retrieved.Content);
        Assert.Equal("markdown", retrieved.Kind);
    }

    [Fact]
    public void Search_FindsViaFts()
    {
        var (_, store) = NewStore();
        store.Create(new Note { Title = "Architecture", WorkspaceId = "ws1", Content = "The flibbertigibbet module handles routing", Source = "manual", Kind = "markdown" });
        store.Create(new Note { Title = "Other", WorkspaceId = "ws1", Content = "unrelated content", Source = "manual", Kind = "markdown" });

        var results = store.Search("ws1", "flibbertigibbet");
        Assert.Single(results);
        Assert.Contains("flibbertigibbet", results[0].Content);
    }

    [Fact]
    public void RebuildIndexFromDisk_RestoresFtsAfterWipe()
    {
        var (dir, store) = NewStore();
        store.Create(new Note { Title = "Rebuildable", WorkspaceId = "ws1", Content = "unique searchable term", Source = "manual", Kind = "markdown" });

        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={System.IO.Path.Combine(dir, "notes", "index.db")}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM notes_index;";
            cmd.ExecuteNonQuery();
        }

        var beforeRebuild = store.Search("ws1", "unique");
        Assert.Empty(beforeRebuild);

        store.RebuildIndexFromDisk();

        var afterRebuild = store.Search("ws1", "unique");
        Assert.Single(afterRebuild);
    }

    [Fact]
    public void Update_ModifiesBodyAndFts()
    {
        var (_, store) = NewStore();
        var note = store.Create(new Note { Title = "Original", WorkspaceId = "ws1", Content = "old content", Source = "manual", Kind = "markdown" });

        store.Update("ws1", note.Id, n => n with { Title = "Updated", Content = "new searchable content" });

        var retrieved = store.Get("ws1", note.Id);
        Assert.Equal("Updated", retrieved!.Title);
        Assert.Equal("new searchable content", retrieved.Content);

        var results = store.Search("ws1", "searchable");
        Assert.Single(results);
    }

    [Fact]
    public void Delete_RemovesFilesAndFts()
    {
        var (dir, store) = NewStore();
        var note = store.Create(new Note { Title = "ToDelete", WorkspaceId = "ws1", Content = "content", Source = "manual", Kind = "markdown" });

        store.Delete("ws1", note.Id);

        Assert.Null(store.Get("ws1", note.Id));
        Assert.False(System.IO.Directory.Exists(System.IO.Path.Combine(dir, "notes", "ws1", note.Id)));
        Assert.Empty(store.Search("ws1", "content"));
    }

    [Fact]
    public void Viewport_PersistsAndLoads()
    {
        var (_, store) = NewStore();
        var note = store.Create(new Note { Title = "Vp", WorkspaceId = "ws1", Content = "c", Source = "manual", Kind = "markdown" });

        store.SaveViewport("ws1", note.Id, """{"scrollX":0,"scrollY":42,"zoom":1.5}""");
        var vp = store.LoadViewport("ws1", note.Id);
        Assert.NotNull(vp);
        Assert.Contains("42", vp);
    }

    [Fact]
    public void State_PersistsAndLoads()
    {
        var (_, store) = NewStore();
        var note = store.Create(new Note { Title = "St", WorkspaceId = "ws1", Content = "c", Source = "manual", Kind = "canvas" });

        store.SaveState("ws1", note.Id, """{"formState":{"name":"test"}}""");
        var state = store.LoadState("ws1", note.Id);
        Assert.NotNull(state);
        Assert.Contains("test", state);
    }

    [Fact]
    public void ListByWorkspace_ReturnsAllNotes()
    {
        var (_, store) = NewStore();
        store.Create(new Note { Title = "A", WorkspaceId = "ws1", Content = "a", Source = "manual", Kind = "markdown" });
        store.Create(new Note { Title = "B", WorkspaceId = "ws1", Content = "b", Source = "manual", Kind = "markdown" });
        store.Create(new Note { Title = "C", WorkspaceId = "ws2", Content = "c", Source = "manual", Kind = "markdown" });

        var ws1 = store.ListByWorkspace("ws1");
        var ws2 = store.ListByWorkspace("ws2");
        Assert.Equal(2, ws1.Count);
        Assert.Single(ws2);
    }
}
