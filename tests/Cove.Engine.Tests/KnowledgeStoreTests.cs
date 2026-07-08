using Cove.Engine.Knowledge;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace Cove.Engine.Tests;

public sealed class NoteStoreTests
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
    public void Create_AssignsId()
    {
        var (dir, store) = NewStore();
        try
        {
            var note = store.Create(new Note { Title = "Test", WorkspaceId = "ws1", Content = "hello", Source = "user:moh" });
            Assert.False(string.IsNullOrEmpty(note.Id));
            Assert.Equal("Test", note.Title);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Get_ReturnsNote()
    {
        var (dir, store) = NewStore();
        try
        {
            var created = store.Create(new Note { Title = "Test", WorkspaceId = "ws1", Content = "hello", Source = "user:moh" });
            var fetched = store.Get("ws1", created.Id);
            Assert.NotNull(fetched);
            Assert.Equal("hello", fetched!.Content);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ListByWorkspace_ReturnsWorkspaceNotes()
    {
        var (dir, store) = NewStore();
        try
        {
            store.Create(new Note { Title = "n1", WorkspaceId = "ws1", Content = "", Source = "u" });
            store.Create(new Note { Title = "n2", WorkspaceId = "ws1", Content = "", Source = "u" });
            store.Create(new Note { Title = "n3", WorkspaceId = "ws2", Content = "", Source = "u" });
            Assert.Equal(2, store.ListByWorkspace("ws1").Count);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Update_ChangesContent()
    {
        var (dir, store) = NewStore();
        try
        {
            var note = store.Create(new Note { Title = "t", WorkspaceId = "ws1", Content = "old", Source = "u" });
            store.Update("ws1", note.Id, n => n with { Content = "new" });
            Assert.Equal("new", store.Get("ws1", note.Id)!.Content);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Delete_RemovesNote()
    {
        var (dir, store) = NewStore();
        try
        {
            var note = store.Create(new Note { Title = "t", WorkspaceId = "ws1", Content = "", Source = "u" });
            store.Delete("ws1", note.Id);
            Assert.Null(store.Get("ws1", note.Id));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }
}
