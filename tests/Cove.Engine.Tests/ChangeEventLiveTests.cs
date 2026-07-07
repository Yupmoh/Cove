using System.Text.Json;
using Cove.Engine.Workspaces;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ChangeEventLiveTests
{
    [Fact]
    public async Task WorkspaceMutation_EmitsStateChangedEvent()
    {
        if (System.OperatingSystem.IsWindows())
            return;
        using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;

        await using var h = await DaemonTestHarness.StartAsync();
        await using FrameConnection ctl = await h.ConnectAsync("gui");

        JsonElement sp = JsonSerializer.SerializeToElement(new WorkspaceCreateParams("evt-proj", "/tmp/evt-proj", null), WorkspacesJsonContext.Default.WorkspaceCreateParams);
        await ctl.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("create", "cove://commands/workspace.create", sp)), ct);

        string? eventChannel = null;
        bool gotResponse = false;
        var deadline = Task.Delay(System.TimeSpan.FromSeconds(10), ct);
        while (!deadline.IsCompleted && (!gotResponse || eventChannel is null))
        {
            Frame f = (await ctl.ReadFrameAsync(ct))!.Value;
            if (f.Header.Type == FrameType.Event)
                eventChannel = ControlCodec.DecodeEvent(f.Payload).Channel;
            else if (f.Header.Type == FrameType.Response)
            {
                var r = ControlCodec.DecodeResponse(f.Payload);
                if (r.Id == "create")
                    gotResponse = true;
            }
        }
        Assert.True(gotResponse, "create response not received");
        Assert.Equal("state.changed", eventChannel);
    }
}
