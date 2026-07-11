using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine;
using Cove.Engine.Bays;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CollectionCommandsTests
{
    private static Task<ControlResponse?> Route(BayManager m, string uri, JsonElement? prm) =>
        EngineCommandRouter.RouteAsync(new ControlRequest("1", uri, prm), null, null, m);

    private static JsonElement El<T>(T v, JsonTypeInfo<T> ti) => JsonSerializer.SerializeToElement(v, ti);

    [Fact]
    public async Task Collections_Default_Synthesis_And_Crud()
    {
        int n = 0;
        await using var m = new BayManager(newId: () => $"id-{++n}");
        var ws1 = await m.CreateBayAsync("a", "/a");
        await m.CreateBayAsync("b", "/b");

        var l0 = await Route(m, "cove://commands/collection.list", null);
        var cols0 = l0!.Data!.Value.GetProperty("collections");
        Assert.Equal(1, cols0.GetArrayLength());
        Assert.Equal("default", cols0[0].GetProperty("id").GetString());
        Assert.Equal("2", cols0[0].GetProperty("projectCount").GetString());

        var created = await Route(m, "cove://commands/collection.create",
            El(new CollectionCreateParams("client-a"), CollectionJsonContext.Default.CollectionCreateParams));
        var cid = created!.Data!.Value.GetProperty("id").GetString()!;

        var moved = await Route(m, "cove://commands/collection.move-bay",
            El(new CollectionMoveParams(ws1.Id, cid), CollectionJsonContext.Default.CollectionMoveParams));
        Assert.True(moved!.Ok);

        var l1 = await Route(m, "cove://commands/collection.list", null);
        var cols1 = l1!.Data!.Value.GetProperty("collections");
        Assert.Equal(2, cols1.GetArrayLength());
        Assert.Equal("1", cols1[0].GetProperty("projectCount").GetString());
        Assert.Equal("client-a", cols1[1].GetProperty("name").GetString());
        Assert.Equal("1", cols1[1].GetProperty("projectCount").GetString());

        var rmDefault = await Route(m, "cove://commands/collection.remove",
            El(new CollectionIdParams("default"), CollectionJsonContext.Default.CollectionIdParams));
        Assert.False(rmDefault!.Ok);

        var rm = await Route(m, "cove://commands/collection.remove",
            El(new CollectionIdParams(cid), CollectionJsonContext.Default.CollectionIdParams));
        Assert.True(rm!.Ok);
        Assert.Equal("default", m.Get(ws1.Id)!.State.CollectionId);

        var l2 = await Route(m, "cove://commands/collection.list", null);
        var cols2 = l2!.Data!.Value.GetProperty("collections");
        Assert.Equal(1, cols2.GetArrayLength());
        Assert.Equal("2", cols2[0].GetProperty("projectCount").GetString());
    }
}
