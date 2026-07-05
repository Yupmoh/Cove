using System.Text.Json;
using Cove.Engine.Workspaces;
using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class WorkspaceModelTests
{
    private static Room MakeRoom(string id, string paneId, string wing = WorkspaceModel.MainWingId) =>
        new() { Id = id, Name = "r", WingId = wing, ActivePaneId = paneId, LayoutTree = new PaneLeaf { PaneId = paneId } };

    private static WorkspaceModel Ws(IReadOnlyList<Room> rooms, IReadOnlyList<Wing>? wings = null, string? active = null)
    {
        var panes = new Dictionary<string, PaneRecord>();
        foreach (var r in rooms)
            panes[r.ActivePaneId!] = new PaneRecord { PaneId = r.ActivePaneId! };
        return new WorkspaceModel
        {
            Id = "ws1",
            Name = "proj",
            ProjectDir = "/tmp/proj",
            Wings = wings ?? [new Wing { Id = WorkspaceModel.MainWingId, Name = "main" }],
            Rooms = rooms,
            Panes = panes,
            ActiveRoomId = active ?? (rooms.Count > 0 ? rooms[0].Id : null),
        };
    }

    private static Func<string> Counter()
    {
        int n = 0;
        return () => $"gen-{++n}";
    }

    [Fact]
    public void CloseRoom_LastRoom_MintsFreshRoom()
    {
        var m = Ws([MakeRoom("r1", "p1")]);
        var next = WorkspaceInvariants.CloseRoom(m, "r1", Counter());
        Assert.Single(next.Rooms);
        Assert.NotEqual("r1", next.Rooms[0].Id);
        Assert.Equal(next.Rooms[0].Id, next.ActiveRoomId);
    }

    [Fact]
    public void CloseRoom_NonLast_RemovesAndRepointsActive()
    {
        var m = Ws([MakeRoom("r1", "p1"), MakeRoom("r2", "p2")], active: "r1");
        var next = WorkspaceInvariants.CloseRoom(m, "r1", Counter());
        Assert.Single(next.Rooms);
        Assert.Equal("r2", next.Rooms[0].Id);
        Assert.Equal("r2", next.ActiveRoomId);
        Assert.False(next.Panes.ContainsKey("p1"));
    }

    [Fact]
    public void RemoveWing_RehomesRoomsToMain()
    {
        var wings = new Wing[] { new() { Id = "main", Name = "main" }, new() { Id = "w2", Name = "side" } };
        var m = Ws([MakeRoom("r1", "p1", "w2")], wings);
        var next = WorkspaceInvariants.RemoveWing(m, "w2", Counter());
        Assert.DoesNotContain(next.Wings, w => w.Id == "w2");
        Assert.Equal("main", next.Rooms[0].WingId);
    }

    [Fact]
    public void RemoveWing_Main_IsNoOp()
    {
        var m = Ws([MakeRoom("r1", "p1")]);
        var next = WorkspaceInvariants.RemoveWing(m, "main", Counter());
        Assert.Same(m, next);
    }

    [Fact]
    public void SwitchWing_EmptyWing_MintsRoom()
    {
        var wings = new Wing[] { new() { Id = "main", Name = "main" }, new() { Id = "w2", Name = "side" } };
        var m = Ws([MakeRoom("r1", "p1", "main")], wings);
        var next = WorkspaceInvariants.SwitchWing(m, "w2", Counter());
        Assert.Equal(2, next.Rooms.Count);
        var minted = next.Rooms.First(r => r.WingId == "w2");
        Assert.Equal(minted.Id, next.ActiveRoomId);
    }

    [Fact]
    public void WorkspaceModel_RoundTrips()
    {
        var m = Ws([MakeRoom("r1", "p1")]);
        var json = JsonSerializer.Serialize(m, WorkspacesJsonContext.Default.WorkspaceModel);
        var back = JsonSerializer.Deserialize(json, WorkspacesJsonContext.Default.WorkspaceModel)!;
        Assert.Equal("ws1", back.Id);
        Assert.Equal("proj", back.Name);
        Assert.Single(back.Rooms);
        Assert.Equal("r1", back.Rooms[0].Id);
        Assert.IsType<PaneLeaf>(back.Rooms[0].LayoutTree);
        Assert.Equal("p1", ((PaneLeaf)back.Rooms[0].LayoutTree).PaneId);
        Assert.True(back.Panes.ContainsKey("p1"));
    }

    [Fact]
    public async Task Fuzz_ConcurrentMutations_NoLostUpdates()
    {
        await using var actor = new Actor<WorkspaceModel>(Ws([]));
        int n = 0;
        var tasks = Enumerable.Range(0, 50).Select(_ => actor.Mutate(m =>
        {
            var id = $"r-{Interlocked.Increment(ref n)}";
            var rooms = new List<Room>(m.Rooms) { MakeRoom(id, "p-" + id) };
            return m with { Rooms = rooms };
        })).ToArray();
        await Task.WhenAll(tasks);
        Assert.Equal(50, actor.State.Rooms.Count);
    }

    [Fact]
    public void RegistryModel_StateJson_RoundTrips()
    {
        var reg = new RegistryModel
        {
            FocusedWorkspaceId = "ws1",
            OpenWorkspaces = ["ws1", "ws2"],
            Collections = [new Collection { Id = "c1", Name = "client-a" }],
            ActiveCollectionId = "c1",
        };
        var json = JsonSerializer.Serialize(reg, WorkspacesJsonContext.Default.RegistryModel);
        var back = JsonSerializer.Deserialize(json, WorkspacesJsonContext.Default.RegistryModel)!;
        Assert.Equal("ws1", back.FocusedWorkspaceId);
        Assert.Equal(2, back.OpenWorkspaces.Count);
        Assert.Single(back.Collections);
        Assert.Equal("client-a", back.Collections[0].Name);
        Assert.Equal("c1", back.ActiveCollectionId);
    }
}
