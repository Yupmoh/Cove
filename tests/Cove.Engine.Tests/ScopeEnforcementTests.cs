using System.Text.Json;
using Cove.Engine.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ScopeEnforcementTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-scope-" + Guid.NewGuid().ToString("N"));

    private static Cove.Engine.Pty.PaneRegistry NewPanes()
        => new(Cove.Platform.Pty.PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance);

    private static string SpawnPane(Cove.Engine.Pty.PaneRegistry panes)
    {
        var req = new Cove.Protocol.ControlRequest("1", "cove://commands/pane.spawn",
            JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"]}""").RootElement.Clone());
        var resp = EngineCommandRouter.RouteAsync(req, panes: panes).GetAwaiter().GetResult();
        return resp!.Data!.Value.GetProperty("paneId").GetString()!;
    }

    [Fact]
    public async Task PaneWrite_CrossPane_SameTabScope_ReturnsAccessDenied()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var panes = NewPanes();
        var scopeStore = new PaneScopeStore(NewDir(), NullLogger.Instance);
        string callerPane = "", targetPane = "";
        try
        {
            callerPane = SpawnPane(panes);
            targetPane = SpawnPane(panes);
            scopeStore.SetScope(callerPane, McpScope.SameTab);
            scopeStore.SetScope(targetPane, McpScope.All);
            var prm = JsonDocument.Parse($"{{\"paneId\":\"{targetPane}\",\"dataBase64\":\"aGk=\"}}").RootElement.Clone();
            var request = new Cove.Protocol.ControlRequest("1", "cove://commands/pane.write", prm, CallerPaneId: callerPane);
            var response = await EngineCommandRouter.RouteAsync(request, panes: panes, paneScopes: scopeStore);
            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("access_denied", response.Error?.Code);
        }
        finally
        {
            try { panes.Kill(callerPane); } catch { }
            try { panes.Kill(targetPane); } catch { }
        }
    }

    [Fact]
    public async Task PaneWrite_SamePane_SameTabScope_Allowed()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var panes = NewPanes();
        var scopeStore = new PaneScopeStore(NewDir(), NullLogger.Instance);
        string pane = "";
        try
        {
            pane = SpawnPane(panes);
            scopeStore.SetScope(pane, McpScope.SameTab);
            var prm = JsonDocument.Parse($"{{\"paneId\":\"{pane}\",\"dataBase64\":\"aGk=\"}}").RootElement.Clone();
            var request = new Cove.Protocol.ControlRequest("1", "cove://commands/pane.write", prm, CallerPaneId: pane);
            var response = await EngineCommandRouter.RouteAsync(request, panes: panes, paneScopes: scopeStore);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
        }
        finally { try { panes.Kill(pane); } catch { } }
    }

    [Fact]
    public async Task PaneWrite_CrossPane_AllScope_Allowed()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var panes = NewPanes();
        var scopeStore = new PaneScopeStore(NewDir(), NullLogger.Instance);
        string callerPane = "", targetPane = "";
        try
        {
            callerPane = SpawnPane(panes);
            targetPane = SpawnPane(panes);
            scopeStore.SetScope(callerPane, McpScope.All);
            var prm = JsonDocument.Parse($"{{\"paneId\":\"{targetPane}\",\"dataBase64\":\"aGk=\"}}").RootElement.Clone();
            var request = new Cove.Protocol.ControlRequest("1", "cove://commands/pane.write", prm, CallerPaneId: callerPane);
            var response = await EngineCommandRouter.RouteAsync(request, panes: panes, paneScopes: scopeStore);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
        }
        finally
        {
            try { panes.Kill(callerPane); } catch { }
            try { panes.Kill(targetPane); } catch { }
        }
    }

    [Fact]
    public async Task PaneWrite_NoCallerPaneId_Allowed()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var panes = NewPanes();
        var scopeStore = new PaneScopeStore(NewDir(), NullLogger.Instance);
        string pane = "";
        try
        {
            pane = SpawnPane(panes);
            scopeStore.SetScope(pane, McpScope.SameTab);
            var prm = JsonDocument.Parse($"{{\"paneId\":\"{pane}\",\"dataBase64\":\"aGk=\"}}").RootElement.Clone();
            var request = new Cove.Protocol.ControlRequest("1", "cove://commands/pane.write", prm);
            var response = await EngineCommandRouter.RouteAsync(request, panes: panes, paneScopes: scopeStore);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
        }
        finally { try { panes.Kill(pane); } catch { } }
    }
}
