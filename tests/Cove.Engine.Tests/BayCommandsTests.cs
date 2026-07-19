using System;
using System.IO;
using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Bays;
using Cove.Engine.Layout;
using Cove.Persistence;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BayCommandsTests
{
    private static Task<ControlResponse?> Route(BayManager manager, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, manager);

    private static Task<ControlResponse?> Route(
        BayManager manager,
        LayoutService layout,
        string uri,
        JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(
            new ControlRequest("1", uri, prm),
            layout: layout,
            bays: manager);

    [Fact]
    public async Task Create_CannotLeaveRegistryAndLayoutWithDifferentActiveBays()
    {
        var existing = new BayModel { Id = "existing", Name = "existing", ProjectDir = "/tmp/existing" };
        var layout = new LayoutService();
        layout.LoadSnapshot(new BaySnapshot { Id = existing.Id, Name = existing.Name, ProjectDir = existing.ProjectDir });
        await using var manager = new BayManager(
            registry: new RegistryModel { OpenBays = [existing.Id] },
            bays: [existing],
            newId: () => "created",
            layout: layout);
        var createParams = JsonSerializer.SerializeToElement(
            new BayCreateParams("created", "/tmp/created"),
            BaysJsonContext.Default.BayCreateParams);

        var response = await Route(manager, layout, "cove://commands/bay.create", createParams);

        Assert.True(response!.Ok);
        Assert.Equal(manager.ActiveBayId, layout.ActiveBayId);
        Assert.Equal("created", layout.ActiveBayId);
    }

    [Fact]
    public async Task Create_EmitsPersistenceMutationOnlyAfterCanonicalStateIsComplete()
    {
        var layout = new LayoutService();
        BayManager? manager = null;
        var observedCompleteState = false;
        manager = new BayManager(
            emit: change =>
            {
                if (change.Kind == BayChangeKind.Created)
                    observedCompleteState =
                        manager!.ActiveBayId == change.BayId
                        && layout.ActiveBayId == change.BayId
                        && layout.BayIds.Contains(change.BayId)
                        && change.ActiveBayId == change.BayId
                        && change.OpenBayIds.Contains(change.BayId);
            },
            newId: () => "created",
            layout: layout);
        await using var ownedManager = manager;
        var createParams = JsonSerializer.SerializeToElement(
            new BayCreateParams("created", "/tmp/created"),
            BaysJsonContext.Default.BayCreateParams);

        var response = await Route(manager, layout, "cove://commands/bay.create", createParams);

        Assert.True(response!.Ok);
        Assert.True(observedCompleteState);
    }

    [Fact]
    public async Task Delete_UnknownManagerBay_DoesNotMutateLayout()
    {
        var layout = new LayoutService();
        layout.EnsureBay("layout-only");
        layout.SetActiveBay("layout-only");
        await using var manager = new BayManager(layout: layout);
        var deleteParams = JsonSerializer.SerializeToElement(
            new BayIdParams("layout-only"),
            BaysJsonContext.Default.BayIdParams);

        var response = await Route(manager, layout, "cove://commands/bay.delete", deleteParams);

        Assert.False(response!.Ok);
        Assert.Contains("layout-only", layout.BayIds);
        Assert.Equal("layout-only", layout.ActiveBayId);
    }

    [Fact]
    public async Task Restore_RebuildsAggregateAndAppliesSavedActiveBayWithoutMutationWrites()
    {
        var mutations = new List<BayChange>();
        var layout = new LayoutService();
        await using var manager = new BayManager(emit: mutations.Add, layout: layout);
        var first = new BaySnapshot
        {
            Id = "first",
            Name = "first",
            ProjectDir = "/tmp/first",
            Shores =
            [
                new ShoreSnapshot
                {
                    Id = "shore-first",
                    Name = "main",
                    LayoutTree = new NookLeaf { NookId = "nook-first" },
                },
            ],
        };
        var second = new BaySnapshot
        {
            Id = "second",
            Name = "second",
            ProjectDir = "/tmp/second",
        };

        await manager.RestoreBayAsync(first, first.Name, first.ProjectDir);
        await manager.RestoreBayAsync(second, second.Name, second.ProjectDir);
        var restoredActive = manager.RestoreActiveBay(first.Id);

        Assert.True(restoredActive);
        Assert.Equal(first.Id, manager.ActiveBayId);
        Assert.Equal(first.Id, layout.ActiveBayId);
        Assert.Equal(new[] { first.Id, second.Id }, manager.Registry.OpenBays);
        Assert.Equal("shore-first", layout.ToSnapshot(first.Id, first.Name, first.ProjectDir).Shores.Single().Id);
        Assert.Empty(mutations);
    }

    [Fact]
    public async Task RestoreActive_UnknownBay_PreservesCanonicalSelection()
    {
        await using var manager = new BayManager();
        var existing = await manager.CreateBayAsync("existing", "/tmp/existing");

        var restored = manager.RestoreActiveBay("missing");

        Assert.False(restored);
        Assert.Equal(existing.Id, manager.ActiveBayId);
    }

    [Fact]
    public async Task LifecycleMutations_EmitOnceAfterEachTransition()
    {
        var mutations = new List<BayChange>();
        var ids = new Queue<string>(["first", "second"]);
        await using var manager = new BayManager(
            emit: mutations.Add,
            newId: ids.Dequeue);

        var first = await manager.CreateBayAsync("first", "/tmp/first");
        var second = await manager.CreateBayAsync("second", "/tmp/second");
        await manager.SwitchBayAsync(first.Id);
        await manager.DeleteBayAsync(first.Id);

        Assert.Equal(
            new[]
            {
                BayChangeKind.Created,
                BayChangeKind.Created,
                BayChangeKind.Switched,
                BayChangeKind.Deleted,
            },
            mutations.Select(change => change.Kind));
        var deleted = mutations[^1];
        Assert.Equal(second.Id, deleted.ActiveBayId);
        Assert.Equal(new[] { second.Id }, deleted.OpenBayIds);
        Assert.Equal(deleted.ActiveBayId, manager.ActiveBayId);
        Assert.Equal(deleted.OpenBayIds.ToArray(), manager.Registry.OpenBays.ToArray());
    }

    [Fact]
    public async Task Create_List_Switch_Delete_WorkHeadless()
    {
        var changes = new List<BayChange>();
        int n = 0;
        await using var manager = new BayManager(emit: changes.Add, newId: () => $"id-{++n}");

        var createParams = JsonSerializer.SerializeToElement(
            new BayCreateParams("proj-a", "/tmp/a"), BaysJsonContext.Default.BayCreateParams);
        var created = await Route(manager, "cove://commands/bay.create", createParams);
        Assert.True(created!.Ok);
        var id = created.Data!.Value.GetProperty("id").GetString()!;

        var listed = await Route(manager, "cove://commands/bay.list", null);
        Assert.True(listed!.Ok);
        var bays = listed.Data!.Value.GetProperty("bays");
        Assert.Equal(1, bays.GetArrayLength());
        Assert.Equal("proj-a", bays[0].GetProperty("name").GetString());
        Assert.True(bays[0].GetProperty("active").GetBoolean());

        var switchParams = JsonSerializer.SerializeToElement(
            new BayIdParams(id), BaysJsonContext.Default.BayIdParams);
        var switched = await Route(manager, "cove://commands/bay.switch", switchParams);
        Assert.True(switched!.Ok);

        var deleted = await Route(manager, "cove://commands/bay.delete", switchParams);
        Assert.True(deleted!.Ok);

        var listed2 = await Route(manager, "cove://commands/bay.list", null);
        Assert.Equal(0, listed2!.Data!.Value.GetProperty("bays").GetArrayLength());

        Assert.Contains(changes, c => c.Kind == BayChangeKind.Created);
        Assert.Contains(changes, c => c.Kind == BayChangeKind.Deleted);
    }

    [Fact]
    public async Task Switch_UnknownBay_Fails()
    {
        await using var manager = new BayManager();
        var prm = JsonSerializer.SerializeToElement(
            new BayIdParams("nope"), BaysJsonContext.Default.BayIdParams);
        var resp = await Route(manager, "cove://commands/bay.switch", prm);
        Assert.False(resp!.Ok);
        Assert.Equal("not_found", resp.Error!.Code);
    }

    [Fact]
    public async Task Create_MissingName_Fails()
    {
        await using var manager = new BayManager();
        var prm = JsonSerializer.SerializeToElement(
            new BayCreateParams("", "/tmp"), BaysJsonContext.Default.BayCreateParams);
        var resp = await Route(manager, "cove://commands/bay.create", prm);
        Assert.False(resp!.Ok);
        Assert.Equal("bad_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Create_MissingPath_Fails()
    {
        await using var manager = new BayManager();
        var prm = JsonSerializer.SerializeToElement(
            new BayCreateParams("proj", ""), BaysJsonContext.Default.BayCreateParams);
        var resp = await Route(manager, "cove://commands/bay.create", prm);
        Assert.False(resp!.Ok);
        Assert.Equal("bad_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Create_MissingPathMember_Fails()
    {
        await using var manager = new BayManager();
        var prm = JsonDocument.Parse("{\"name\":\"proj\"}").RootElement.Clone();
        var resp = await Route(manager, "cove://commands/bay.create", prm);
        Assert.False(resp!.Ok);
        Assert.Equal("bad_params", resp.Error!.Code);
    }

    [Fact]
    public async Task Create_CreatesDirectory_AndPersistsResolvedPath()
    {
        await using var manager = new BayManager();
        var dir = Path.Combine(Path.GetTempPath(), "cove-create-" + Guid.NewGuid().ToString("N"), "nested");
        try
        {
            Assert.False(Directory.Exists(dir));
            var prm = JsonSerializer.SerializeToElement(
                new BayCreateParams("proj", dir), BaysJsonContext.Default.BayCreateParams);
            var resp = await Route(manager, "cove://commands/bay.create", prm);
            Assert.True(resp!.Ok);
            Assert.True(Directory.Exists(dir));

            var listed = await Route(manager, "cove://commands/bay.list", null);
            var ws = listed!.Data!.Value.GetProperty("bays")[0];
            Assert.Equal(Path.GetFullPath(dir), ws.GetProperty("projectDir").GetString());
        }
        finally { Cove.Testing.TestDirectory.Delete(Path.GetDirectoryName(dir)!); }
    }

    [Fact]
    public async Task Create_ExpandsTilde()
    {
        await using var manager = new BayManager();
        var leaf = "cove-tilde-" + Guid.NewGuid().ToString("N");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = Path.Combine(home, leaf);
        try
        {
            var prm = JsonSerializer.SerializeToElement(
                new BayCreateParams("proj", "~/" + leaf), BaysJsonContext.Default.BayCreateParams);
            var resp = await Route(manager, "cove://commands/bay.create", prm);
            Assert.True(resp!.Ok);
            var listed = await Route(manager, "cove://commands/bay.list", null);
            var ws = listed!.Data!.Value.GetProperty("bays")[0];
            Assert.Equal(expected, ws.GetProperty("projectDir").GetString());
        }
        finally { Cove.Testing.TestDirectory.Delete(expected); }
    }
}
