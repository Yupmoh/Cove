using System.Linq;
using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Layout;
using Cove.Engine.Workspaces;
using Cove.Persistence;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class WorkspaceLayoutCoordinationTests
{
    private static Task<ControlResponse?> Route(WorkspaceManager m, LayoutService layout, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, layout, m);

    private static JsonElement Create(string name, string dir) =>
        JsonSerializer.SerializeToElement(new WorkspaceCreateParams(name, dir), WorkspacesJsonContext.Default.WorkspaceCreateParams);

    private static JsonElement Id(string id) =>
        JsonSerializer.SerializeToElement(new WorkspaceIdParams(id), WorkspacesJsonContext.Default.WorkspaceIdParams);

    [Fact]
    public async Task Create_MakesWorkspaceActiveInLayout_AndEmpty()
    {
        int n = 0;
        await using var mgr = new WorkspaceManager(newId: () => $"id-{++n}");
        var layout = new LayoutService();

        var created = await Route(mgr, layout, "cove://commands/workspace.create", Create("proj", "/tmp/a"));
        var id = created!.Data!.Value.GetProperty("id").GetString()!;

        Assert.Equal(id, layout.ActiveWorkspaceId);
        var snap = layout.ToSnapshot(id, "proj", "/tmp/a");
        Assert.Empty(snap.Rooms);
    }

    [Fact]
    public async Task Switch_SwapsActiveLayoutWorkspace()
    {
        int n = 0;
        await using var mgr = new WorkspaceManager(newId: () => $"id-{++n}");
        var layout = new LayoutService();

        var a = (await Route(mgr, layout, "cove://commands/workspace.create", Create("A", "/tmp/a")))!.Data!.Value.GetProperty("id").GetString()!;
        var b = (await Route(mgr, layout, "cove://commands/workspace.create", Create("B", "/tmp/b")))!.Data!.Value.GetProperty("id").GetString()!;

        await Route(mgr, layout, "cove://commands/workspace.switch", Id(a));
        Assert.Equal(a, layout.ActiveWorkspaceId);
        await Route(mgr, layout, "cove://commands/workspace.switch", Id(b));
        Assert.Equal(b, layout.ActiveWorkspaceId);
    }

    [Fact]
    public async Task LayoutGet_ReflectsActiveWorkspaceIdentity()
    {
        int n = 0;
        await using var mgr = new WorkspaceManager(newId: () => $"id-{++n}");
        var layout = new LayoutService();

        var a = (await Route(mgr, layout, "cove://commands/workspace.create", Create("Alpha", "/tmp/alpha")))!.Data!.Value.GetProperty("id").GetString()!;

        var get = await Route(mgr, layout, "cove://commands/layout.get", null);
        var snap = get!.Data!.Value.Deserialize(Cove.Persistence.CoveJsonContext.Default.WorkspaceSnapshot)!;
        Assert.Equal(a, snap.Id);
        Assert.Equal("Alpha", snap.Name);
        Assert.Equal("/tmp/alpha", snap.ProjectDir);
    }

    [Fact]
    public async Task Delete_RemovesLayoutWorkspace()
    {
        int n = 0;
        await using var mgr = new WorkspaceManager(newId: () => $"id-{++n}");
        var layout = new LayoutService();

        var a = (await Route(mgr, layout, "cove://commands/workspace.create", Create("A", "/tmp/a")))!.Data!.Value.GetProperty("id").GetString()!;
        layout.CreateRoom("Terminal 1", new PaneLeaf { PaneId = "p1", Subtabs = new[] { new Subtab("p1", PaneType.Terminal) } });

        await Route(mgr, layout, "cove://commands/workspace.delete", Id(a));
        Assert.DoesNotContain(a, layout.WorkspaceIds);
    }
}
