using System.Text.Json;
using Cove.Adapters;
using Cove.Engine.Agents;
using Cove.Engine.Layout;
using Cove.Engine.Launch;
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

public sealed class AgentLaunchCommandTests
{
    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task NewLaunch_SpawnsPlacesAndRegistersAgentAtomically()
    {
        using var nooks = NewNooks();
        var layout = new LayoutService();
        layout.SetActiveBay("bay-1");
        var shoreId = layout.CreateShore("Main", Leaf("anchor"));
        layout.FocusNook(shoreId, "anchor");
        var agents = new AgentMessageRouter();
        var sessions = new SessionResumeOrchestrator();
        var scopes = NewScopes();
        var launcher = NewLauncher();
        var request = Request(new AgentLaunchParams(
            "new",
            "test",
            "default",
            null,
            "/tmp",
            "anchor",
            "right",
            "bay-1",
            "Worker",
            false,
            80,
            24,
            "same-tab"));

        var response = await EngineCommandRouter.RouteAsync(
            request,
            nooks: nooks,
            layout: layout,
            agentRouter: agents,
            sessions: sessions,
            launcher: launcher,
            nookScopes: scopes);

        Assert.True(response!.Ok, response.Error?.Message);
        var result = response.Data!.Value.Deserialize(
            Cove.Protocol.CoveJsonContext.Default.AgentLaunchResult)!;
        Assert.Contains(
            nooks.List(),
            nook => nook.NookId == result.NookId);
        Assert.Equal(
            ("bay-1", shoreId),
            layout.ResolveNookLocation(result.NookId));
        Assert.Equal("test", agents.ResolveTarget(result.NookId)!.Adapter);
        Assert.NotNull(sessions.GetState(result.NookId));
        Assert.Equal(McpScope.SameTab, scopes.GetScope(result.NookId));
        Assert.Equal("right", result.Placement);
        Assert.False(result.Resumed);
        nooks.Kill(result.NookId);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task ResumeLaunch_UsesRequestedAdapterAndSession()
    {
        using var nooks = NewNooks();
        var adapter = new RecordingResumeAdapter();
        var launcher = NewLauncher(new AgentResumeService(adapter));
        var layout = new LayoutService();
        layout.SetActiveBay("bay-1");
        var shoreId = layout.CreateShore("Main", Leaf("anchor"));
        var request = Request(new AgentLaunchParams(
            "resume",
            "test",
            "default",
            "session-7",
            "/tmp",
            "anchor",
            "right",
            "bay-1",
            null,
            true,
            80,
            24,
            "same-bay"));

        var response = await EngineCommandRouter.RouteAsync(
            request,
            nooks: nooks,
            layout: layout,
            agentRouter: new AgentMessageRouter(),
            sessions: new SessionResumeOrchestrator(),
            launcher: launcher,
            nookScopes: NewScopes());

        Assert.True(response!.Ok, response.Error?.Message);
        var result = response.Data!.Value.Deserialize(
            Cove.Protocol.CoveJsonContext.Default.AgentLaunchResult)!;
        Assert.Equal("test", adapter.Adapter);
        Assert.Equal("session-7", adapter.SessionId);
        Assert.True(result.Resumed);
        Assert.Equal("session-7", result.SessionId);
        nooks.Kill(result.NookId);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task MissingPlacementTarget_LeavesNoProcessOrMetadata()
    {
        using var nooks = NewNooks();
        var agents = new AgentMessageRouter();
        var sessions = new SessionResumeOrchestrator();
        var layout = new LayoutService();
        layout.SetActiveBay("bay-1");
        var request = Request(new AgentLaunchParams(
            "new",
            "test",
            "default",
            null,
            "/tmp",
            "missing",
            "right",
            "bay-1",
            null,
            false,
            80,
            24,
            "same-bay"));

        var response = await EngineCommandRouter.RouteAsync(
            request,
            nooks: nooks,
            layout: layout,
            agentRouter: agents,
            sessions: sessions,
            launcher: NewLauncher(),
            nookScopes: NewScopes());

        Assert.False(response!.Ok);
        Assert.Empty(nooks.List());
        Assert.Empty(agents.List());
    }

    [Fact]
    public async Task NookCaller_CannotLaunchAcrossItsScope()
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
        var request = Request(
            new AgentLaunchParams(
                "new",
                "test",
                "default",
                null,
                "/tmp",
                "target",
                "right",
                "bay-b",
                null,
                false,
                80,
                24,
                "same-bay"),
            "caller");

        var response = await EngineCommandRouter.RouteAsync(
            request,
            nooks: nooks,
            layout: layout,
            launcher: NewLauncher(),
            nookScopes: scopes);

        Assert.False(response!.Ok);
        Assert.Equal("access_denied", response.Error?.Code);
        Assert.Empty(nooks.List());
    }

    private static ControlRequest Request(
        AgentLaunchParams parameters,
        string? callerNookId = null) => new(
        "launch",
        "cove://commands/agent.launch",
        JsonSerializer.SerializeToElement(
            parameters,
            Cove.Protocol.CoveJsonContext.Default.AgentLaunchParams),
        CallerNookId: callerNookId);

    private static NookRegistry NewNooks() => new(
        PtyHostFactory.Create(NullLogger.Instance),
        NullLogger.Instance);

    private static NookScopeStore NewScopes() => new(
        Path.Combine(
            Path.GetTempPath(),
            "cove-launch-scope-" + Guid.NewGuid().ToString("N")),
        NullLogger.Instance);

    private static LaunchOrchestrator NewLauncher(
        AgentResumeService? resume = null) => new(
        new LaunchCommandComposer(),
        profiles: new ProfileLookup(),
        resumeService: resume);

    private static NookLeaf Leaf(string nookId) => new()
    {
        NookId = nookId,
        Subtabs = [new Subtab(nookId, NookType.Terminal)],
    };

    private sealed class ProfileLookup : ILaunchProfileLookup
    {
        public LaunchProfile? Find(string adapter, string profileSlug) => new(
            "Default",
            profileSlug,
            adapter,
            true,
            null,
            null,
            ["/bin/sleep", "30"],
            new Dictionary<string, string>(),
            new Dictionary<string, bool>(),
            [],
            null,
            1);
    }

    private sealed class RecordingResumeAdapter : IAdapterResume
    {
        public string? Adapter { get; private set; }
        public string? SessionId { get; private set; }

        public Task<ResumeCommand> BuildResumeCommandAsync(
            string adapter,
            string sessionId,
            LauncherOverrides overrides,
            CancellationToken cancellationToken)
        {
            Adapter = adapter;
            SessionId = sessionId;
            return Task.FromResult(new ResumeCommand(
                "/bin/sleep",
                ["30"],
                overrides.WorkingDir ?? ""));
        }

        public Task WaitForReadiness(
            string sessionId,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public bool IsSessionReaped(string sessionId) => false;
    }
}
