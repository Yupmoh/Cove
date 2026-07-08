using Cove.Engine.Knowledge;
using Cove.Protocol;
using Xunit;
namespace Cove.Engine.Tests;

public sealed class NoteStoreTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-notes-" + System.Guid.NewGuid().ToString("N"));

    [Fact]
    public void Create_AssignsId()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new NoteStore(dir);
            var note = store.Create(new Note { Title = "Test", WorkspaceId = "ws1", Content = "hello", Source = "user:moh" });
            Assert.False(string.IsNullOrEmpty(note.Id));
            Assert.Equal("Test", note.Title);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Get_ReturnsNote()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new NoteStore(dir);
            var created = store.Create(new Note { Title = "Test", WorkspaceId = "ws1", Content = "hello", Source = "user:moh" });
            var fetched = store.Get(created.Id);
            Assert.NotNull(fetched);
            Assert.Equal("hello", fetched!.Content);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ListByWorkspace_ReturnsWorkspaceNotes()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new NoteStore(dir);
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
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new NoteStore(dir);
            var note = store.Create(new Note { Title = "t", WorkspaceId = "ws1", Content = "old", Source = "u" });
            store.Update(note.Id, n => n with { Content = "new" });
            Assert.Equal("new", store.Get(note.Id)!.Content);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Delete_RemovesNote()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new NoteStore(dir);
            var note = store.Create(new Note { Title = "t", WorkspaceId = "ws1", Content = "", Source = "u" });
            store.Delete(note.Id);
            Assert.Null(store.Get(note.Id));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }
}
