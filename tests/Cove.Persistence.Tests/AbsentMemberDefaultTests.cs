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
        Assert.NotNull(s.OpenWorkspaces);
        Assert.Empty(s.OpenWorkspaces);
    }

    [Fact]
    public void CoveState_PresentMembers_Roundtrip()
    {
        var s = JsonSerializer.Deserialize("{\"schemaVersion\":5,\"openWorkspaces\":[\"a\"]}", CoveJsonContext.Default.CoveState)!;
        Assert.Equal(5, s.SchemaVersion);
        Assert.Equal(new[] { "a" }, s.OpenWorkspaces);
    }

    [Fact]
    public void WorkspaceSnapshot_AbsentMembers_KeepDefaults()
    {
        var json = "{\"id\":\"w\",\"name\":\"n\",\"projectDir\":\"/p\",\"rooms\":[{\"id\":\"r\",\"name\":\"rn\",\"layoutTree\":{\"kind\":\"leaf\",\"paneId\":\"p1\"}}]}";
        var w = JsonSerializer.Deserialize(json, CoveJsonContext.Default.WorkspaceSnapshot)!;
        Assert.Equal(1, w.SchemaVersion);
        Assert.NotNull(w.Rooms);
        Assert.Single(w.Rooms);
    }

    [Fact]
    public void PaneLeaf_AbsentSubtabs_KeepEmpty()
    {
        var leaf = (PaneLeaf)JsonSerializer.Deserialize("{\"kind\":\"leaf\",\"paneId\":\"p1\"}", CoveJsonContext.Default.MosaicNode)!;
        Assert.NotNull(leaf.Subtabs);
        Assert.Empty(leaf.Subtabs);
    }

    [Fact]
    public void SplitNode_AbsentRatio_KeepsHalf()
    {
        var json = "{\"kind\":\"split\",\"orientation\":0,\"childA\":{\"kind\":\"leaf\",\"paneId\":\"a\"},\"childB\":{\"kind\":\"leaf\",\"paneId\":\"b\"}}";
        var split = (SplitNode)JsonSerializer.Deserialize(json, CoveJsonContext.Default.MosaicNode)!;
        Assert.Equal(0.5, split.Ratio);
    }
}
