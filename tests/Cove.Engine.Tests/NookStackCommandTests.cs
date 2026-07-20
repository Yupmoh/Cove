using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Engine.Protocol;
using Cove.Persistence;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookStackCommandTests
{
    [Fact]
    public async Task Stack_BalancesPlacedTargetAndClearsZoom()
    {
        var layout = new LayoutService();
        layout.SetActiveBay("bay-1");
        var shoreId = layout.CreateShore("Main", Leaf("a"));
        layout.SplitNook(
            shoreId,
            "a",
            SplitOrientation.Column,
            Leaf("b"));
        layout.SplitNook(
            shoreId,
            "b",
            SplitOrientation.Column,
            Leaf("c"));
        layout.FocusNook(shoreId, "c");
        layout.SetZoom(shoreId, "c");
        var changes = 0;
        layout.OnChanged += () => changes++;

        var response = await EngineCommandRouter.RouteAsync(
            Request("c", "below"),
            layout: layout);

        Assert.True(response!.Ok, response.Error?.Message);
        var result = response.Data!.Value.Deserialize(
            Cove.Protocol.CoveJsonContext.Default.NookStackResult)!;
        Assert.Equal("c", result.NookId);
        Assert.Equal("bay-1", result.BayId);
        Assert.Equal(shoreId, result.ShoreId);
        Assert.Equal("below", result.Placement);
        Assert.Equal(3, result.Nooks);
        Assert.Equal(1, changes);
        var snapshot = layout.ToSnapshot("bay-1", "Cove", "/tmp");
        Assert.Null(Assert.Single(snapshot.Shores).ZoomedNookId);
    }

    [Theory]
    [InlineData("sideways")]
    [InlineData("")]
    public async Task Stack_InvalidPlacementDoesNotMutate(
        string placement)
    {
        var layout = LayoutWithRow(out var shoreId);
        var root = layout.GetRoot(shoreId);

        var response = await EngineCommandRouter.RouteAsync(
            Request("b", placement),
            layout: layout);

        Assert.False(response!.Ok);
        Assert.Equal("invalid_params", response.Error?.Code);
        Assert.Same(root, layout.GetRoot(shoreId));
    }

    [Fact]
    public async Task Stack_UnplacedTargetReturnsNotFound()
    {
        var layout = LayoutWithRow(out _);

        var response = await EngineCommandRouter.RouteAsync(
            Request("missing", "right"),
            layout: layout);

        Assert.False(response!.Ok);
        Assert.Equal("not_found", response.Error?.Code);
    }

    [Fact]
    public async Task Stack_MissingAxisReturnsInvalidStateWithoutMutation()
    {
        var layout = LayoutWithRow(out var shoreId);
        var root = layout.GetRoot(shoreId);

        var response = await EngineCommandRouter.RouteAsync(
            Request("b", "below"),
            layout: layout);

        Assert.False(response!.Ok);
        Assert.Equal("invalid_state", response.Error?.Code);
        Assert.Same(root, layout.GetRoot(shoreId));
    }

    [Fact]
    public async Task Stack_CallerCannotCrossItsScope()
    {
        var layout = new LayoutService();
        layout.SetActiveBay("bay-a");
        var callerShore = layout.CreateShore(
            "Caller",
            Leaf("caller"));
        layout.FocusNook(callerShore, "caller");
        layout.SetActiveBay("bay-b");
        var targetShore = layout.CreateShore(
            "Target",
            Leaf("target-a"));
        layout.SplitNook(
            targetShore,
            "target-a",
            SplitOrientation.Column,
            Leaf("target-b"));
        var scopes = NewScopes();
        scopes.SetScope("caller", McpScope.SameBay);

        var response = await EngineCommandRouter.RouteAsync(
            Request("target-b", "below", "caller"),
            layout: layout,
            nookScopes: scopes);

        Assert.False(response!.Ok);
        Assert.Equal("access_denied", response.Error?.Code);
    }

    private static ControlRequest Request(
        string nookId,
        string placement,
        string? callerNookId = null) => new(
        "stack",
        "cove://commands/nook.stack",
        JsonSerializer.SerializeToElement(
            new NookStackParams(nookId, placement),
            Cove.Protocol.CoveJsonContext.Default.NookStackParams),
        CallerNookId: callerNookId);

    private static LayoutService LayoutWithRow(out string shoreId)
    {
        var layout = new LayoutService();
        layout.SetActiveBay("bay-1");
        shoreId = layout.CreateShore("Main", Leaf("a"));
        layout.SplitNook(
            shoreId,
            "a",
            SplitOrientation.Row,
            Leaf("b"));
        layout.FocusNook(shoreId, "b");
        return layout;
    }

    private static NookScopeStore NewScopes() => new(
        Path.Combine(
            Path.GetTempPath(),
            "cove-stack-scope-" + Guid.NewGuid().ToString("N")),
        NullLogger.Instance);

    private static SplitNode Split(
        SplitOrientation orientation,
        MosaicNode childA,
        MosaicNode childB) => new()
        {
            Orientation = orientation,
            ChildA = childA,
            ChildB = childB,
        };

    private static NookLeaf Leaf(string nookId) => new()
    {
        NookId = nookId,
        Subtabs = [new Subtab(nookId, NookType.Terminal)],
    };
}
