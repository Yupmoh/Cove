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

public sealed class TimelineStoreTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-timeline-" + System.Guid.NewGuid().ToString("N"));

    [Fact]
    public void Append_AssignsIdAndTimestamp()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new TimelineStore(dir);
            var entry = store.Append(new TimelineEntry { WorkspaceId = "ws1", Kind = "note.created", Source = "user:moh" });
            Assert.False(string.IsNullOrEmpty(entry.Id));
            Assert.True(entry.Timestamp > System.DateTimeOffset.MinValue);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ListByWorkspace_ReturnsEntries()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new TimelineStore(dir);
            store.Append(new TimelineEntry { WorkspaceId = "ws1", Kind = "a", Source = "u" });
            store.Append(new TimelineEntry { WorkspaceId = "ws1", Kind = "b", Source = "u" });
            store.Append(new TimelineEntry { WorkspaceId = "ws2", Kind = "c", Source = "u" });
            Assert.Equal(2, store.ListByWorkspace("ws1").Count);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ListByWorkspace_OrderedDescendingByTimestamp()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new TimelineStore(dir);
            var e1 = store.Append(new TimelineEntry { WorkspaceId = "ws1", Kind = "first", Source = "u" });
            System.Threading.Thread.Sleep(10);
            var e2 = store.Append(new TimelineEntry { WorkspaceId = "ws1", Kind = "second", Source = "u" });
            var entries = store.ListByWorkspace("ws1");
            Assert.Equal("second", entries[0].Kind);
            Assert.Equal("first", entries[1].Kind);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }
}
