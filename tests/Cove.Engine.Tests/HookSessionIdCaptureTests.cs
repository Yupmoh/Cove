using System.Text.Json;
using Cove.Adapters;
using Cove.Engine.Hooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HookSessionIdCaptureTests
{
    private static JsonElement Payload(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void SessionStart_WithSessionId_CapturesOntoPaneState_AndFiresCallback()
    {
        var router = new HookEventRouter();
        (string pane, string adapter, string session)? captured = null;
        router.SessionStarted += (pane, adapter, session) => captured = (pane, adapter, session);

        router.Route(new HookEvent
        {
            Adapter = "claude-code",
            Event = "session-start",
            PaneId = "pane-1",
            Payload = Payload("{\"session_id\":\"sess-abc\",\"cwd\":\"/repo\"}"),
        });

        Assert.NotNull(captured);
        Assert.Equal(("pane-1", "claude-code", "sess-abc"), captured!.Value);
        Assert.Equal("sess-abc", router.GetPaneState("pane-1")!.SessionId);
    }

    [Fact]
    public void SessionId_SurvivesSubsequentEvents()
    {
        var router = new HookEventRouter();
        router.Route(new HookEvent
        {
            Adapter = "claude-code",
            Event = "session-start",
            PaneId = "pane-1",
            Payload = Payload("{\"session_id\":\"sess-abc\"}"),
        });
        router.Route(new HookEvent { Adapter = "claude-code", Event = "stop", PaneId = "pane-1" });

        Assert.Equal("sess-abc", router.GetPaneState("pane-1")!.SessionId);
    }

    [Fact]
    public void SessionStart_WithoutSessionId_DoesNotFireCallback()
    {
        var router = new HookEventRouter();
        var fired = false;
        router.SessionStarted += (_, _, _) => fired = true;

        router.Route(new HookEvent { Adapter = "claude-code", Event = "session-start", PaneId = "pane-1" });

        Assert.False(fired);
        Assert.Null(router.GetPaneState("pane-1")!.SessionId);
    }
}
