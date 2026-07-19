using System;
using System.IO;
using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Bays;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BayCommandsTests
{
    private static Task<ControlResponse?> Route(BayManager manager, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, manager);

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
