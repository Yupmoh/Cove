using System.Text.Json;
using Cove.Persistence;
using Xunit;

namespace Cove.Persistence.Tests;

public class AbsentMemberDefaultTests
{
    [Fact]
    public void CoveState_AbsentMembers_KeepDefaults()
    {
        var s = JsonSerializer.Deserialize("{}", CoveJsonContext.Default.CoveState)!;
        Assert.Equal(1, s.SchemaVersion);
        Assert.NotNull(s.OpenBays);
        Assert.Empty(s.OpenBays);
    }

    [Fact]
    public void CoveState_PresentMembers_Roundtrip()
    {
        var s = JsonSerializer.Deserialize("{\"schemaVersion\":5,\"openBays\":[\"a\"]}", CoveJsonContext.Default.CoveState)!;
        Assert.Equal(5, s.SchemaVersion);
        Assert.Equal(new[] { "a" }, s.OpenBays);
    }

    [Fact]
    public void BaySnapshot_AbsentMembers_KeepDefaults()
    {
        var json = "{\"id\":\"w\",\"name\":\"n\",\"projectDir\":\"/p\",\"shores\":[{\"id\":\"r\",\"name\":\"rn\",\"layoutTree\":{\"kind\":\"leaf\",\"nookId\":\"p1\"}}]}";
        var w = JsonSerializer.Deserialize(json, CoveJsonContext.Default.BaySnapshot)!;
        Assert.Equal(1, w.SchemaVersion);
        Assert.NotNull(w.Shores);
        Assert.Single(w.Shores);
    }

    [Fact]
    public void NookLeaf_AbsentSubtabs_KeepEmpty()
    {
        var leaf = (NookLeaf)JsonSerializer.Deserialize("{\"kind\":\"leaf\",\"nookId\":\"p1\"}", CoveJsonContext.Default.MosaicNode)!;
        Assert.NotNull(leaf.Subtabs);
        Assert.Empty(leaf.Subtabs);
    }

    [Fact]
    public void SplitNode_AbsentRatio_KeepsHalf()
    {
        var json = "{\"kind\":\"split\",\"orientation\":0,\"childA\":{\"kind\":\"leaf\",\"nookId\":\"a\"},\"childB\":{\"kind\":\"leaf\",\"nookId\":\"b\"}}";
        var split = (SplitNode)JsonSerializer.Deserialize(json, CoveJsonContext.Default.MosaicNode)!;
        Assert.Equal(0.5, split.Ratio);
    }
}
