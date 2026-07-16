using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Engine.Restart;
using Cove.Platform.Ipc;
using Cove.Platform.Pty.Unix;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HandoffNegotiationTests
{
    [Fact]
    public async Task HandoffBegin_TransfersLiveNooksAndShutsPredecessorDown()
    {
        if (OperatingSystem.IsWindows()) return;
        await using var harness = await DaemonTestHarness.StartAsync();
        var conn = await harness.ConnectAsync("cli");
        var spawn = JsonSerializer.SerializeToElement(
            new SpawnParams("/bin/sh", new[] { "-c", "sleep 300" }, "/tmp"), CoveJsonContext.Default.SpawnParams);
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://commands/nook.spawn", spawn)), CancellationToken.None);
        var spawnResponse = ControlCodec.DecodeResponse((await conn.ReadFrameAsync(CancellationToken.None))!.Value.Payload);
        Assert.True(spawnResponse.Ok);

        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("3", "cove://handoff/begin", null)), CancellationToken.None);
        var beginResponse = ControlCodec.DecodeResponse((await conn.ReadFrameAsync(CancellationToken.None))!.Value.Payload);
        Assert.True(beginResponse.Ok);
        var begin = beginResponse.Data!.Value.Deserialize(CoveJsonContext.Default.HandoffBeginResult)!;
        Assert.Equal(1, begin.NookCount);

        var received = new List<(HandoffNookRecord Record, int Fd, byte[] Ring)>();
        using (var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
        {
            socket.Connect(new UnixDomainSocketEndPoint(begin.SocketPath));
            var socketFd = (int)socket.Handle;
            for (var i = 0; i < begin.NookCount; i++)
            {
                var item = HandoffWire.ReadRecord(socketFd);
                Assert.NotNull(item);
                received.Add(item!.Value);
            }
            socket.Send(new[] { (byte)'K' });
        }

        Assert.Equal("/bin/sh", received[0].Record.Command);
        Assert.True(received[0].Record.Pid > 0);
        Assert.True(received[0].Fd >= 0);
        foreach (var item in received)
            UnixFdChannel.CloseFd(item.Fd);

        await harness.Run.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task HandoffBegin_SecondCallConflicts()
    {
        if (OperatingSystem.IsWindows()) return;
        await using var harness = await DaemonTestHarness.StartAsync();
        var conn = await harness.ConnectAsync("cli");
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://handoff/begin", null)), CancellationToken.None);
        var first = ControlCodec.DecodeResponse((await conn.ReadFrameAsync(CancellationToken.None))!.Value.Payload);
        Assert.True(first.Ok);

        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("3", "cove://handoff/begin", null)), CancellationToken.None);
        var second = ControlCodec.DecodeResponse((await conn.ReadFrameAsync(CancellationToken.None))!.Value.Payload);
        Assert.False(second.Ok);
        Assert.Equal("conflict", second.Error?.Code);
    }
}
