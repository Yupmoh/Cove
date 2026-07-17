using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Platform.Pty.Unix;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Restart;

public sealed record HandoffTakeoverItem(HandoffNookRecord Record, int Fd, byte[] Ring);

public sealed record HandoffTakeover(IReadOnlyList<HandoffTakeoverItem> Items);

public static class HandoffClient
{
    private const int ConnectTimeoutMs = 2000;
    private const int PredecessorExitTimeoutMs = 15000;

    public static async Task<HandoffTakeover?> TryTakeOverAsync(DaemonPaths paths, IControlEndpoint endpoint, ILogger logger, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
            return null;

        Stream stream;
        try
        {
            stream = await endpoint.ConnectAsync(ConnectTimeoutMs, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            logger.HandoffNoPredecessor(paths.Channel);
            return null;
        }

        var items = new List<HandoffTakeoverItem>();
        var conn = new FrameConnection(stream);
        try
        {
            var hello = JsonSerializer.SerializeToElement(
                new HelloParams(ProtocolConstants.SemanticProtocolVersion, "handoff", CoveBuild.InformationalVersion, paths.Channel),
                CoveJsonContext.Default.HelloParams);
            await conn.WriteFrameAsync(FrameType.Request, 0,
                ControlCodec.Encode(new ControlRequest("1", "cove://sys/hello", hello)), cancellationToken).ConfigureAwait(false);
            if (await conn.ReadFrameAsync(cancellationToken).ConfigureAwait(false) is not { } helloFrame
                || !ControlCodec.DecodeResponse(helloFrame.Payload).Ok)
            {
                logger.HandoffBeginRejected(paths.Channel, "hello failed");
                return null;
            }

            await conn.WriteFrameAsync(FrameType.Request, 0,
                ControlCodec.Encode(new ControlRequest("2", "cove://handoff/begin", null)), cancellationToken).ConfigureAwait(false);
            if (await conn.ReadFrameAsync(cancellationToken).ConfigureAwait(false) is not { } beginFrame)
            {
                logger.HandoffBeginRejected(paths.Channel, "no begin response");
                return null;
            }
            var beginResponse = ControlCodec.DecodeResponse(beginFrame.Payload);
            if (!beginResponse.Ok || beginResponse.Data is not { } data)
            {
                logger.HandoffBeginRejected(paths.Channel, beginResponse.Error?.Message ?? "begin refused");
                return null;
            }
            var begin = data.Deserialize(CoveJsonContext.Default.HandoffBeginResult);
            if (begin is null)
            {
                logger.HandoffBeginRejected(paths.Channel, "begin payload malformed");
                return null;
            }

            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Connect(new UnixDomainSocketEndPoint(begin.SocketPath));
            var socketFd = (int)socket.Handle;

            for (var i = 0; i < begin.NookCount; i++)
            {
                if (HandoffWire.ReadRecord(socketFd) is not { } received)
                {
                    logger.HandoffTransferTruncated(paths.Channel, i, begin.NookCount);
                    CloseAll(items);
                    return null;
                }
                items.Add(new HandoffTakeoverItem(received.Record, received.Fd, received.Ring));
            }

            socket.Send(new[] { (byte)'K' });
            if (!await WaitForPredecessorExitAsync(paths, cancellationToken).ConfigureAwait(false))
            {
                logger.HandoffPredecessorLingered(paths.Channel);
                CloseAll(items);
                return null;
            }

            logger.HandoffReceived(paths.Channel, items.Count);
            return new HandoffTakeover(items);
        }
        catch (Exception ex)
        {
            logger.HandoffTakeoverFailed(paths.Channel, ex.Message);
            CloseAll(items);
            return null;
        }
        finally
        {
            await conn.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static void CloseAll(List<HandoffTakeoverItem> items)
    {
        foreach (var item in items)
            UnixFdChannel.CloseFd(item.Fd);
    }

    private static async Task<bool> WaitForPredecessorExitAsync(DaemonPaths paths, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < PredecessorExitTimeoutMs)
        {
            if (PidFile.Read(paths.PidFilePath) is not { } pid)
                return true;
            try
            {
                Process.GetProcessById(pid);
            }
            catch (ArgumentException)
            {
                return true;
            }
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        return false;
    }
}
