using System.Text.Json;
using Cove.Engine.Agents;
using Cove.Engine.Layout;
using Cove.Engine.Protocol;
using Cove.Engine.Pty;
using Cove.Engine.Sessions;
using Cove.Persistence;
using Cove.Platform.Pty;
using Cove.Protocol;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class WorkspaceContextCommandTests
{
    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Context_ReturnsLiveWorkspaceAgentAndSessionState()
    {
        using var nooks = new NookRegistry(
            PtyHostFactory.Create(NullLogger.Instance),
            NullLogger.Instance);
        var spawn = await EngineCommandRouter.RouteAsync(
            new ControlRequest(
                "spawn",
                "cove://commands/nook.spawn",
                JsonDocument.Parse(
                    """{"command":"/bin/sh","args":["-c","sleep 30"],"cwd":"/tmp"}""")
                    .RootElement.Clone()),
            nooks: nooks);
        var nookId = spawn!.Data!.Value.GetProperty("nookId").GetString()!;
        try
        {
            var layout = new LayoutService();
            layout.SetActiveBay("bay-live");
            var staleShoreId = layout.CreateShore(
                "Stale",
                Leaf("other-nook"));
            var liveShoreId = layout.CreateShore(
                "Live",
                Leaf(nookId));
            layout.SwitchShore("bay-live", liveShoreId);
            layout.FocusNook(liveShoreId, nookId);
            var agents = new AgentMessageRouter();
            agents.Register(
                nookId,
                "omp",
                "Worker",
                "bay-stale",
                staleShoreId);
            var sessions = new SessionResumeOrchestrator();
            sessions.Register(nookId, "omp", "session-42");
            var scopes = new NookScopeStore(
                Path.Combine(
                    Path.GetTempPath(),
                    "cove-context-" + Guid.NewGuid().ToString("N")),
                NullLogger.Instance);
            scopes.SetScope(nookId, McpScope.All);
            var request = new ControlRequest(
                "context",
                "cove://commands/workspace.context",
                JsonSerializer.SerializeToElement(
                    new WorkspaceContextParams(nookId),
                    Cove.Protocol.CoveJsonContext.Default.WorkspaceContextParams));

            var response = await EngineCommandRouter.RouteAsync(
                request,
                nooks: nooks,
                layout: layout,
                agentRouter: agents,
                sessions: sessions,
                nookScopes: scopes,
                getWorkspaceRevision: () => 17);

            Assert.True(response!.Ok, response.Error?.Message);
            var context = response.Data!.Value.Deserialize(
                Cove.Protocol.CoveJsonContext.Default.WorkspaceContextResult)!;
            Assert.Equal(nookId, context.NookId);
            Assert.Equal("omp", context.Adapter);
            Assert.Equal("session-42", context.SessionId);
            Assert.Equal("bay-live", context.BayId);
            Assert.Equal(liveShoreId, context.ShoreId);
            Assert.Equal(nookId, context.FocusedNookId);
            Assert.Equal("bay-live", context.ActiveBayId);
            Assert.Equal(liveShoreId, context.ActiveShoreId);
            Assert.Equal(17, context.LayoutRevision);
            Assert.Equal("/tmp", context.Cwd);
            Assert.Equal("all", context.EffectiveAccessScope);
        }
        finally
        {
            nooks.Kill(nookId);
        }
    }

    private static NookLeaf Leaf(string nookId) => new()
    {
        NookId = nookId,
        Subtabs = [new Subtab(nookId, NookType.Terminal)],
    };
}
