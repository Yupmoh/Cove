using System.Text.Json;
using Cove.Engine.Browser;
using Cove.Engine.Layout;
using Cove.Engine.Protocol;
using Cove.Engine.Pty;
using Cove.Persistence;
using Cove.Platform.Pty;
using Cove.Protocol;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookOpenCommandTests
{
    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task ExplicitCommand_SpawnsAndPlacesTerminalAtomically()
    {
        using var nooks = NewNooks();
        var layout = LayoutWithAnchor(out var shoreId);
        var response = await EngineCommandRouter.RouteAsync(
            Request(new NookOpenParams(
                "terminal",
                "/bin/cat",
                [],
                "/tmp",
                "anchor",
                "right",
                "bay-1",
                100,
                30)),
            nooks: nooks,
            layout: layout,
            nookScopes: NewScopes());

        Assert.True(response!.Ok, response.Error?.Message);
        var result = response.Data!.Value.Deserialize(
            Cove.Protocol.CoveJsonContext.Default.NookOpenResult)!;
        try
        {
            var opened = Assert.Single(
                nooks.List(),
                nook => nook.NookId == result.NookId);
            Assert.Equal("/bin/cat", opened.Command);
            Assert.Equal("terminal", result.NookType);
            Assert.Equal("bay-1", result.BayId);
            Assert.Equal(shoreId, result.ShoreId);
            Assert.Equal("right", result.Placement);
            Assert.Equal(("bay-1", shoreId), layout.ResolveNookLocation(result.NookId));
            var split = Assert.IsType<SplitNode>(layout.GetRoot(shoreId));
            Assert.Equal(SplitOrientation.Row, split.Orientation);
            Assert.Equal("anchor", Assert.IsType<NookLeaf>(split.ChildA).NookId);
            Assert.Equal(result.NookId, Assert.IsType<NookLeaf>(split.ChildB).NookId);
            Assert.Equal(result.NookId, layout.FocusedNookFor("bay-1"));
        }
        finally
        {
            nooks.Kill(result.NookId);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task MissingCommand_UsesDefaultShellAndBelowPlacement()
    {
        using var nooks = NewNooks();
        var layout = LayoutWithAnchor(out var shoreId);
        var response = await EngineCommandRouter.RouteAsync(
            Request(new NookOpenParams(
                "terminal",
                null,
                [],
                "/tmp",
                "anchor",
                "below",
                "bay-1")),
            nooks: nooks,
            layout: layout,
            nookScopes: NewScopes());

        Assert.True(response!.Ok, response.Error?.Message);
        var result = response.Data!.Value.Deserialize(
            Cove.Protocol.CoveJsonContext.Default.NookOpenResult)!;
        try
        {
            var opened = Assert.Single(
                nooks.List(),
                nook => nook.NookId == result.NookId);
            Assert.False(string.IsNullOrWhiteSpace(opened.Command));
            var split = Assert.IsType<SplitNode>(layout.GetRoot(shoreId));
            Assert.Equal(SplitOrientation.Column, split.Orientation);
            Assert.Equal("anchor", Assert.IsType<NookLeaf>(split.ChildA).NookId);
            Assert.Equal(result.NookId, Assert.IsType<NookLeaf>(split.ChildB).NookId);
        }
        finally
        {
            nooks.Kill(result.NookId);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task MissingPlacementTarget_LeavesNoProcessOrLayoutLeaf()
    {
        using var nooks = NewNooks();
        var layout = LayoutWithAnchor(out var shoreId);
        var response = await EngineCommandRouter.RouteAsync(
            Request(new NookOpenParams(
                "terminal",
                "/bin/cat",
                [],
                "/tmp",
                "missing",
                "right",
                "bay-1")),
            nooks: nooks,
            layout: layout,
            nookScopes: NewScopes());

        Assert.False(response!.Ok);
        Assert.Equal("not_found", response.Error?.Code);
        Assert.Empty(nooks.List());
        Assert.Equal("anchor", Assert.IsType<NookLeaf>(layout.GetRoot(shoreId)).NookId);
    }

    [Fact]
    public async Task MissingExplicitCwdReturnsInvalidCwdWithoutProcessOrLayoutMutation()
    {
        using var nooks = NewNooks();
        var layout = LayoutWithAnchor(out var shoreId);
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var response = await EngineCommandRouter.RouteAsync(
            Request(new NookOpenParams(
                "terminal",
                null,
                [],
                missing,
                "anchor",
                "right",
                "bay-1")),
            nooks: nooks,
            layout: layout,
            nookScopes: NewScopes());

        Assert.False(response!.Ok);
        Assert.Equal("invalid_cwd", response.Error?.Code);
        Assert.Empty(nooks.List());
        Assert.Equal("anchor", Assert.IsType<NookLeaf>(layout.GetRoot(shoreId)).NookId);
    }

    [Fact]
    public async Task NookCaller_CannotOpenAcrossItsScope()
    {
        using var nooks = NewNooks();
        var layout = new LayoutService();
        layout.SetActiveBay("bay-a");
        var callerShore = layout.CreateShore("Caller", Leaf("caller"));
        layout.FocusNook(callerShore, "caller");
        layout.SetActiveBay("bay-b");
        layout.CreateShore("Target", Leaf("target"));
        var scopes = NewScopes();
        scopes.SetScope("caller", McpScope.SameBay);
        var response = await EngineCommandRouter.RouteAsync(
            Request(
                new NookOpenParams(
                    "terminal",
                    null,
                    [],
                    "/tmp",
                    "target",
                    "right",
                    "bay-b"),
                "caller"),
            nooks: nooks,
            layout: layout,
            nookScopes: scopes);

        Assert.False(response!.Ok);
        Assert.Equal("access_denied", response.Error?.Code);
        Assert.Empty(nooks.List());
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task BrowserWithoutUrl_UsesDefaultAndPlacesBrowserLeaf()
    {
        using var nooks = NewNooks();
        var browser = new BrowserNookManager();
        var layout = LayoutWithAnchor(out var shoreId);
        var response = await EngineCommandRouter.RouteAsync(
            Request(new NookOpenParams(
                "browser",
                null,
                [],
                "/tmp",
                "anchor",
                "below",
                "bay-1",
                Url: null)),
            nooks: nooks,
            layout: layout,
            browser: browser,
            nookScopes: NewScopes());

        Assert.True(response!.Ok, response.Error?.Message);
        var result = response.Data!.Value.Deserialize(
            Cove.Protocol.CoveJsonContext.Default.NookOpenResult)!;
        var opened = browser.Get(result.NookId);
        Assert.NotNull(opened);
        Assert.Equal("https://duckduckgo.com", opened.CurrentUrl);
        Assert.Equal("browser", result.NookType);
        Assert.Empty(nooks.List());
        var split = Assert.IsType<SplitNode>(layout.GetRoot(shoreId));
        Assert.Equal(SplitOrientation.Column, split.Orientation);
        var leaf = Assert.IsType<NookLeaf>(split.ChildB);
        Assert.Equal(result.NookId, leaf.NookId);
        Assert.Equal(NookType.Browser, Assert.Single(leaf.Subtabs).NookType);
        Assert.Equal(result.NookId, layout.FocusedNookFor("bay-1"));
        browser.Close(result.NookId);
    }

    private static ControlRequest Request(
        NookOpenParams parameters,
        string? callerNookId = null) => new(
        "open",
        "cove://commands/nook.open",
        JsonSerializer.SerializeToElement(
            parameters,
            Cove.Protocol.CoveJsonContext.Default.NookOpenParams),
        CallerNookId: callerNookId);

    private static LayoutService LayoutWithAnchor(out string shoreId)
    {
        var layout = new LayoutService();
        layout.SetActiveBay("bay-1");
        shoreId = layout.CreateShore("Main", Leaf("anchor"));
        layout.FocusNook(shoreId, "anchor");
        return layout;
    }

    private static NookRegistry NewNooks() => new(
        PtyHostFactory.Create(NullLogger.Instance),
        NullLogger.Instance);

    private static NookScopeStore NewScopes() => new(
        Path.Combine(
            Path.GetTempPath(),
            "cove-nook-open-scope-" + Guid.NewGuid().ToString("N")),
        NullLogger.Instance);

    private static NookLeaf Leaf(string nookId) => new()
    {
        NookId = nookId,
        Subtabs = [new Subtab(nookId, NookType.Terminal)],
    };
}
