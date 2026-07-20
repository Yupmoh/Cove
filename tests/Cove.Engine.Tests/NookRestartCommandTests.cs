using System.Text;
using System.Text.Json;
using Cove.Adapters;
using Cove.Engine.Agents;
using Cove.Engine.Hooks;
using Cove.Engine.Layout;
using Cove.Engine.Launch;
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

public sealed class NookRestartCommandTests
{
    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task FreshRestart_PreservesIdentityLayoutAndScrollback()
    {
        using var nooks = NewNooks();
        var nook = nooks.Spawn(new SpawnParams(
            "/bin/sh",
            ["-c", "printf before; sleep 30"],
            "/tmp"));
        var layout = new LayoutService();
        layout.SetActiveBay("bay-1");
        var shoreId = layout.CreateShore("Main", Leaf(nook.NookId));
        await WaitForOutput(nooks, nook.NookId, "before");
        var request = Request(new NookRestartParams(
            nook.NookId,
            "fresh",
            true));

        var response = await EngineCommandRouter.RouteAsync(
            request,
            nooks: nooks,
            layout: layout);

        Assert.True(response!.Ok, response.Error?.Message);
        var result = response.Data!.Value.Deserialize(
            Cove.Protocol.CoveJsonContext.Default.NookRestartResult)!;
        Assert.Equal(nook.NookId, result.NookId);
        Assert.True(result.PreservedScrollbackBytes > 0);
        Assert.Equal(
            ("bay-1", shoreId),
            layout.ResolveNookLocation(nook.NookId));
        Assert.Single(nooks.List());
        Assert.Contains(
            "before",
            Encoding.UTF8.GetString(
                nooks.Read(nook.NookId, 0, 65536)));
        nooks.Kill(nook.NookId);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task ResumeCurrent_UsesCurrentAdapterSessionAndSameNook()
    {
        using var nooks = NewNooks();
        var nook = nooks.Spawn(new SpawnParams(
            "/bin/sleep",
            ["30"],
            "/tmp",
            Adapter: "test"));
        var agents = new AgentMessageRouter();
        agents.Register(nook.NookId, "test", "Worker");
        var sessions = new SessionResumeOrchestrator();
        sessions.Register(nook.NookId, "test", "session-9");
        var adapter = new RecordingResumeAdapter();
        var hooks = new HookEventRouter();
        hooks.Seed(
            nook.NookId,
            "test",
            "session-9",
            "working");
        var request = Request(new NookRestartParams(
            nook.NookId,
            "resume-current",
            true));

        var response = await EngineCommandRouter.RouteAsync(
            request,
            nooks: nooks,
            agentRouter: agents,
            sessions: sessions,
            launcher: NewLauncher(adapter),
            hookRouter: hooks);

        Assert.True(response!.Ok, response.Error?.Message);
        Assert.Equal("test", adapter.Adapter);
        Assert.Equal("session-9", adapter.SessionId);
        Assert.Single(nooks.List());
        Assert.Equal(
            nook.NookId,
            nooks.List()[0].NookId);
        var hookState = hooks.GetNookState(nook.NookId)!;
        Assert.Equal("idle", hookState.Status);
        Assert.Equal("session-9", hookState.SessionId);
        nooks.Kill(nook.NookId);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task FailedReplacement_LeavesOriginalProcessAndMetadata()
    {
        using var nooks = NewNooks();
        var nook = nooks.Spawn(new SpawnParams(
            "/bin/sleep",
            ["30"],
            "/tmp",
            Adapter: "test"));
        var agents = new AgentMessageRouter();
        agents.Register(nook.NookId, "test", "Worker");
        var sessions = new SessionResumeOrchestrator();
        sessions.Register(nook.NookId, "test", "session-original");
        var request = Request(new NookRestartParams(
            nook.NookId,
            "fresh",
            true));

        var response = await EngineCommandRouter.RouteAsync(
            request,
            nooks: nooks,
            agentRouter: agents,
            sessions: sessions,
            launcher: NewLauncher(
                profileCommand: "/definitely/missing/cove-agent"));

        Assert.False(response!.Ok);
        var original = Assert.Single(nooks.List());
        Assert.Equal(nook.NookId, original.NookId);
        Assert.True(original.Alive);
        Assert.Equal(
            "test",
            agents.ResolveTarget(nook.NookId)!.Adapter);
        Assert.Equal(
            "session-original",
            sessions.GetState(nook.NookId)!.SessionId);
        nooks.Kill(nook.NookId);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task CommandRestart_IsRestrictedToControlCallers()
    {
        using var nooks = NewNooks();
        var nook = nooks.Spawn(new SpawnParams(
            "/bin/sleep",
            ["30"],
            "/tmp"));
        var parameters = new NookRestartParams(
            nook.NookId,
            "command",
            true,
            "/bin/sh",
            ["-c", "printf replacement; sleep 30"]);

        var denied = await EngineCommandRouter.RouteAsync(
            Request(parameters, nook.NookId),
            nooks: nooks);
        var allowed = await EngineCommandRouter.RouteAsync(
            Request(parameters),
            nooks: nooks);

        Assert.False(denied!.Ok);
        Assert.Equal("forbidden", denied.Error!.Code);
        Assert.True(allowed!.Ok, allowed.Error?.Message);
        await WaitForOutput(
            nooks,
            nook.NookId,
            "replacement");
        Assert.Single(nooks.List());
        nooks.Kill(nook.NookId);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task ResumeFailure_UsesOnlyRequestedFreshFallback()
    {
        using var nooks = NewNooks();
        var nook = nooks.Spawn(new SpawnParams(
            "/bin/sleep",
            ["30"],
            "/tmp",
            Adapter: "test"));
        var agents = new AgentMessageRouter();
        agents.Register(nook.NookId, "test", "Worker");
        var sessions = new SessionResumeOrchestrator();
        sessions.Register(nook.NookId, "test", "session-9");
        var adapter = new RecordingResumeAdapter
        {
            FailReadiness = true,
        };

        var noFallback = await EngineCommandRouter.RouteAsync(
            Request(new NookRestartParams(
                nook.NookId,
                "resume-current",
                true)),
            nooks: nooks,
            agentRouter: agents,
            sessions: sessions,
            launcher: NewLauncher(adapter));
        var fallback = await EngineCommandRouter.RouteAsync(
            Request(new NookRestartParams(
                nook.NookId,
                "resume-current",
                true,
                ResumeFallback: "fresh")),
            nooks: nooks,
            agentRouter: agents,
            sessions: sessions,
            launcher: NewLauncher(adapter));

        Assert.False(noFallback!.Ok);
        Assert.Equal(
            "resume_failed",
            noFallback.Error!.Code);
        Assert.True(fallback!.Ok, fallback.Error?.Message);
        var result = fallback.Data!.Value.Deserialize(
            Cove.Protocol.CoveJsonContext.Default
                .NookRestartResult)!;
        Assert.Equal("fallback-fresh", result.Outcome);
        Assert.True(result.FallbackUsed);
        Assert.Null(result.SessionId);
        Assert.Single(nooks.List());
        nooks.Kill(nook.NookId);
    }

    private static ControlRequest Request(
        NookRestartParams parameters,
        string? callerNookId = null) => new(
        "restart",
        "cove://commands/nook.restart",
        JsonSerializer.SerializeToElement(
            parameters,
            Cove.Protocol.CoveJsonContext.Default.NookRestartParams),
        CallerNookId: callerNookId);

    private static NookRegistry NewNooks() => new(
        PtyHostFactory.Create(NullLogger.Instance),
        NullLogger.Instance);

    private static LaunchOrchestrator NewLauncher(
        IAdapterResume? adapter = null,
        string profileCommand = "/bin/sleep") => new(
        new LaunchCommandComposer(),
        profiles: new ProfileLookup(profileCommand),
        resumeService: adapter is null
            ? null
            : new AgentResumeService(adapter));

    private static NookLeaf Leaf(string nookId) => new()
    {
        NookId = nookId,
        Subtabs = [new Subtab(nookId, NookType.Terminal)],
    };

    private static async Task WaitForOutput(
        NookRegistry nooks,
        string nookId,
        string expected)
    {
        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));
        while (!cts.IsCancellationRequested)
        {
            if (Encoding.UTF8.GetString(
                    nooks.Read(nookId, 0, 65536))
                .Contains(expected, StringComparison.Ordinal))
            {
                return;
            }
            await Task.Delay(20, cts.Token);
        }
    }

    private sealed class ProfileLookup(string command)
        : ILaunchProfileLookup
    {
        public LaunchProfile? Find(
            string adapter,
            string profileSlug) => new(
            "Default",
            profileSlug,
            adapter,
            true,
            null,
            null,
            [command, "30"],
            new Dictionary<string, string>(),
            new Dictionary<string, bool>(),
            [],
            null,
            1);

        public LaunchProfile Resolve(string adapter) =>
            Find(adapter, "default")!;
    }

    private sealed class RecordingResumeAdapter : IAdapterResume
    {
        public bool FailReadiness { get; init; }
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
            CancellationToken cancellationToken)
        {
            if (FailReadiness)
            {
                throw new ResumeFailedException(
                    "session unavailable");
            }
            return Task.CompletedTask;
        }

        public bool IsSessionReaped(string sessionId) => false;
    }
}
