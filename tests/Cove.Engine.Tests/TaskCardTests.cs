using Cove.Engine.Tasks;
using Cove.Protocol;
using Xunit;
namespace Cove.Engine.Tests;

public sealed class TaskCardTests
{
    [Fact]
    public void Create_SetsDefaults()
    {
        var card = new TaskCard
        {
            Title = "Test task",
            WorkspaceId = "ws1",
            Source = "user:moh",
        };
        Assert.Equal("Test task", card.Title);
        Assert.Equal("ws1", card.WorkspaceId);
        Assert.Equal("user:moh", card.Source);
        Assert.Equal(TaskPriority.Medium, card.Priority);
        Assert.Equal(TaskSize.M, card.Size);
        Assert.Equal("medium", card.Priority.ToString().ToLowerInvariant());
    }

    [Fact]
    public void TaskId_FormatIsCoveN()
    {
        var card = new TaskCard { Title = "t", WorkspaceId = "ws1", Source = "user:moh", TaskNumber = 42 };
        Assert.Equal("COVE-42", card.HumanId);
    }

    [Fact]
    public void Priority_Ordering_CriticalHighest()
    {
        Assert.True(TaskPriority.Critical > TaskPriority.High);
        Assert.True(TaskPriority.High > TaskPriority.Medium);
        Assert.True(TaskPriority.Medium > TaskPriority.Low);
    }

    [Fact]
    public void Status_Transitions_Valid()
    {
        var card = new TaskCard { Title = "t", WorkspaceId = "ws1", Source = "user:moh" };
        Assert.Equal("todo", card.StatusId);
        card.StatusId = "in-progress";
        Assert.Equal("in-progress", card.StatusId);
    }
}

public sealed class TaskStoreTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-tasks-" + System.Guid.NewGuid().ToString("N"));

    [Fact]
    public void Create_AssignsTaskNumber()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new TaskStore(dir);
            var card = store.Create(new TaskCard { Title = "t1", WorkspaceId = "ws1", Source = "user:moh" });
            Assert.Equal(1, card.TaskNumber);
            Assert.Equal("COVE-1", card.HumanId);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Create_IncrementsTaskNumber()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new TaskStore(dir);
            var c1 = store.Create(new TaskCard { Title = "t1", WorkspaceId = "ws1", Source = "user:moh" });
            var c2 = store.Create(new TaskCard { Title = "t2", WorkspaceId = "ws1", Source = "user:moh" });
            Assert.Equal(1, c1.TaskNumber);
            Assert.Equal(2, c2.TaskNumber);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Get_ReturnsCard()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new TaskStore(dir);
            var created = store.Create(new TaskCard { Title = "t1", WorkspaceId = "ws1", Source = "user:moh" });
            var fetched = store.Get(created.Id);
            Assert.NotNull(fetched);
            Assert.Equal("t1", fetched!.Title);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Get_Nonexistent_ReturnsNull()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new TaskStore(dir);
            Assert.Null(store.Get("nonexistent"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ListByWorkspace_ReturnsWorkspaceCards()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new TaskStore(dir);
            store.Create(new TaskCard { Title = "t1", WorkspaceId = "ws1", Source = "user:moh" });
            store.Create(new TaskCard { Title = "t2", WorkspaceId = "ws1", Source = "user:moh" });
            store.Create(new TaskCard { Title = "t3", WorkspaceId = "ws2", Source = "user:moh" });
            var ws1 = store.ListByWorkspace("ws1");
            Assert.Equal(2, ws1.Count);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Update_ChangesTitle()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new TaskStore(dir);
            var card = store.Create(new TaskCard { Title = "t1", WorkspaceId = "ws1", Source = "user:moh" });
            store.Update(card.Id, c => c with { Title = "updated" });
            var fetched = store.Get(card.Id);
            Assert.Equal("updated", fetched!.Title);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Delete_RemovesCard()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new TaskStore(dir);
            var card = store.Create(new TaskCard { Title = "t1", WorkspaceId = "ws1", Source = "user:moh" });
            store.Delete(card.Id);
            Assert.Null(store.Get(card.Id));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ResolveByHumanId_ReturnsCard()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new TaskStore(dir);
            store.Create(new TaskCard { Title = "t1", WorkspaceId = "ws1", Source = "user:moh" });
            var card = store.ResolveByHumanId("COVE-1");
            Assert.NotNull(card);
            Assert.Equal("t1", card!.Title);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }
}
