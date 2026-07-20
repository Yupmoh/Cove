using System.Text.Json;
using Cove.Engine.Agents;
using Cove.Engine.Browser;
using Cove.Engine.Layout;
using Cove.Engine.Launch;
using Cove.Engine.Lifecycle;
using Cove.Engine.Protocol;
using Cove.Engine.Pty;
using Cove.Engine.Restart;
using Cove.Engine.Sessions;
using Cove.Persistence;
using Cove.Platform.Pty;
using Cove.Protocol;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookCloseCommandTests
{
    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task TerminalAgent_CloseRemovesRuntimeLayoutAndMetadata()
    {
        using var nooks = NewNooks();
        var layout = LayoutWithAnchor(out var shoreId);
        var opened = await OpenAsync(
            new NookOpenParams(
                "terminal",
                "/bin/cat",
                [],
                "/tmp",
                "anchor",
                "right",
                "bay-1"),
            nooks,
            layout);
        var agents = new AgentMessageRouter();
        agents.Register(opened.NookId, "omp", "Worker", "bay-1", shoreId);
        var sessions = new SessionResumeOrchestrator();
        sessions.Register(opened.NookId, "omp", "session-1");
        var lifecycle = new AgentLifecycleController();
        lifecycle.Register(opened.NookId, "omp");
        var scopes = NewScopes();
        scopes.SetScope(opened.NookId, McpScope.SameTab);
        var launcher = new LaunchOrchestrator(new LaunchCommandComposer());
        launcher.PersistOverrides(opened.NookId, new LauncherOverrides { Yolo = true });

        var response = await EngineCommandRouter.RouteAsync(
            Request(opened.NookId),
            nooks: nooks,
            layout: layout,
            agentRouter: agents,
            sessions: sessions,
            lifecycle: lifecycle,
            launcher: launcher,
            nookScopes: scopes);

        Assert.True(response!.Ok, response.Error?.Message);
        var result = response.Data!.Value.Deserialize(
            Cove.Protocol.CoveJsonContext.Default.NookCloseResult)!;
        Assert.Equal("terminal", result.NookType);
        Assert.Equal("bay-1", result.BayId);
        Assert.Equal(shoreId, result.ShoreId);
        Assert.DoesNotContain(nooks.List(), nook => nook.NookId == opened.NookId);
        Assert.Null(layout.ResolveNookLocation(opened.NookId).BayId);
        Assert.Null(agents.ResolveTarget(opened.NookId));
        Assert.Null(sessions.GetState(opened.NookId));
        Assert.Null(launcher.GetOverrides(opened.NookId));
        Assert.Equal(McpScope.SameBay, scopes.GetScope(opened.NookId));
        Assert.Equal(LifecycleState.Closed, lifecycle.GetState(opened.NookId)!.State);
        Assert.Equal("anchor", Assert.IsType<NookLeaf>(layout.GetRoot(shoreId)).NookId);
        Assert.Equal("anchor", layout.FocusedNookFor("bay-1"));
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Browser_CloseRemovesBrowserStateAndLayout()
    {
        using var nooks = NewNooks();
        var browser = new BrowserNookManager();
        var layout = LayoutWithAnchor(out var shoreId);
        var opened = await OpenAsync(
            new NookOpenParams(
                "browser",
                null,
                [],
                null,
                "anchor",
                "below",
                "bay-1",
                Url: "about:blank"),
            nooks,
            layout,
            browser);

        var response = await EngineCommandRouter.RouteAsync(
            Request(opened.NookId),
            nooks: nooks,
            layout: layout,
            browser: browser,
            nookScopes: NewScopes());

        Assert.True(response!.Ok, response.Error?.Message);
        var result = response.Data!.Value.Deserialize(
            Cove.Protocol.CoveJsonContext.Default.NookCloseResult)!;
        Assert.Equal("browser", result.NookType);
        Assert.Null(browser.Get(opened.NookId));
        Assert.Null(layout.ResolveNookLocation(opened.NookId).BayId);
        Assert.Equal("anchor", Assert.IsType<NookLeaf>(layout.GetRoot(shoreId)).NookId);
        Assert.Equal("anchor", layout.FocusedNookFor("bay-1"));
        Assert.Empty(nooks.List());
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task UnknownTarget_DoesNotMutateRuntimeOrLayout()
    {
        using var nooks = NewNooks();
        var browser = new BrowserNookManager();
        var layout = LayoutWithAnchor(out var shoreId);

        var response = await EngineCommandRouter.RouteAsync(
            Request("missing"),
            nooks: nooks,
            layout: layout,
            browser: browser,
            nookScopes: NewScopes());

        Assert.False(response!.Ok);
        Assert.Equal("not_found", response.Error?.Code);
        Assert.Equal("anchor", Assert.IsType<NookLeaf>(layout.GetRoot(shoreId)).NookId);
        Assert.Empty(nooks.List());
    }

    private static ControlRequest Request(string nookId) => new(
        "close",
        "cove://commands/nook.close",
        JsonSerializer.SerializeToElement(
            new NookRefParams(nookId),
            Cove.Protocol.CoveJsonContext.Default.NookRefParams));

    private static async Task<NookOpenResult> OpenAsync(
        NookOpenParams parameters,
        NookRegistry nooks,
        LayoutService layout,
        BrowserNookManager? browser = null)
    {
        var response = await EngineCommandRouter.RouteAsync(
            new ControlRequest(
                "open",
                "cove://commands/nook.open",
                JsonSerializer.SerializeToElement(
                    parameters,
                    Cove.Protocol.CoveJsonContext.Default.NookOpenParams)),
            nooks: nooks,
            layout: layout,
            browser: browser,
            nookScopes: NewScopes());
        Assert.True(response!.Ok, response.Error?.Message);
        return response.Data!.Value.Deserialize(
            Cove.Protocol.CoveJsonContext.Default.NookOpenResult)!;
    }

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
            "cove-nook-close-scope-" + Guid.NewGuid().ToString("N")),
        NullLogger.Instance);

    private static NookLeaf Leaf(string nookId) => new()
    {
        NookId = nookId,
        Subtabs = [new Subtab(nookId, NookType.Terminal)],
    };
}
