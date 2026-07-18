using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Cove.Engine.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Daemon;

internal sealed class NookStreamRouter
{
    private readonly DaemonPaths _paths;
    private readonly NookRegistry _nooks;
    private readonly ILogger _logger;

    public NookStreamRouter(
        DaemonPaths paths,
        NookRegistry nooks,
        ILogger logger)
    {
        _paths = paths;
        _nooks = nooks;
        _logger = logger;
    }

    public async Task StreamAsync(
        FrameConnection connection,
        Stream stream,
        ControlRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Params is not JsonElement element
            || element.Deserialize(CoveJsonContext.Default.SubscribeParams) is not { } parameters)
        {
            await WriteResponseAsync(
                connection,
                Fail(request.Id, "invalid_params", "subscribe params required"),
                cancellationToken).ConfigureAwait(false);
            return;
        }
        if (!_nooks.TryGet(parameters.NookId, out var nook))
        {
            _logger.SubscribeUnknownNook(parameters.NookId);
            await WriteResponseAsync(
                connection,
                Fail(request.Id, "not_found", $"unknown nook {parameters.NookId}"),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        const ulong streamId = 1;
        var head = nook.Ring.Head;
        var tail = nook.Ring.Tail;
        var checkpoint = _nooks.GetTerminalCheckpoint(parameters.NookId);
        var useCheckpoint = checkpoint is not null
            && ((long)parameters.SinceOffset < checkpoint.Offset
                || (parameters.SinceOffset == 0 && checkpoint.Offset == 0));
        var baseOffset = useCheckpoint
            ? checkpoint!.Offset
            : Math.Clamp((long)parameters.SinceOffset, tail, head);
        _logger.SubscribeStarted(parameters.NookId, baseOffset, head, tail);
        var result = new SubscribeResult(
            streamId,
            (ulong)baseOffset,
            ProtocolConstants.FlowWindow,
            (ulong)head,
            Convert.ToBase64String(
                Encoding.ASCII.GetBytes(
                    useCheckpoint
                        ? checkpoint!.ModeSupplement
                        : nook.Reader.TerminalModePreamble)),
            useCheckpoint ? Convert.ToBase64String(checkpoint!.Data) : "",
            useCheckpoint ? checkpoint!.Cols : 0,
            useCheckpoint ? checkpoint!.Rows : 0);
        await WriteResponseAsync(
            connection,
            new ControlResponse(
                request.Id,
                true,
                JsonSerializer.SerializeToElement(
                    result,
                    CoveJsonContext.Default.SubscribeResult)),
            cancellationToken).ConfigureAwait(false);

        if (_nooks.ConsumePendingRepaint(parameters.NookId))
        {
            var repaintCols = nook.Cols;
            var repaintRows = nook.Rows;
            _ = Task.Run(async () =>
            {
                try
                {
                    _nooks.Resize(
                        parameters.NookId,
                        Math.Max(2, repaintCols - 1),
                        repaintRows);
                    await Task.Delay(50).ConfigureAwait(false);
                    _nooks.Resize(parameters.NookId, repaintCols, repaintRows);
                    DaemonLog.Write(
                        _paths,
                        $"repaint jiggle after adoption nook={parameters.NookId} cols={repaintCols} rows={repaintRows}");
                }
                catch (Exception ex)
                {
                    DaemonLog.Write(
                        _paths,
                        $"repaint jiggle failed nook={parameters.NookId}: {ex.Message}");
                }
            });
        }

        var sink = new SocketByteStreamSink(stream);
        var sender = new PtyStreamSender(
            streamId,
            nook.Session.SessionId,
            nook.Ring,
            baseOffset,
            sink,
            parameters.NookId,
            _logger,
            () => nook.Reader.TerminalModePreamble,
            () =>
            {
                var currentCheckpoint = _nooks.GetTerminalCheckpoint(parameters.NookId);
                return currentCheckpoint is null
                    ? null
                    : new TerminalResyncSnapshot(
                        currentCheckpoint.Offset,
                        currentCheckpoint.Data,
                        currentCheckpoint.Cols,
                        currentCheckpoint.Rows,
                        currentCheckpoint.ModeSupplement);
            });
        var gate = new object();
        var childMarked = false;

        using var streamCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var creditLoop = Task.Run(async () =>
        {
            try
            {
                while (!streamCancellation.IsCancellationRequested)
                {
                    var maybe = await connection.ReadFrameAsync(
                        streamCancellation.Token).ConfigureAwait(false);
                    if (maybe is null)
                        break;
                    var frame = maybe.Value;
                    if (frame.Header.Type == FrameType.Credit
                        && frame.Payload.Length >= 8)
                    {
                        var acknowledged =
                            BinaryPrimitives.ReadUInt64LittleEndian(frame.Payload);
                        lock (gate)
                            sender.OnCredit(acknowledged);
                    }
                    nook.Signal.Set();
                }
            }
            catch (Exception ex)
            {
                _logger.SubscribeCreditLoopClosed(parameters.NookId, ex.Message);
            }
            finally
            {
                streamCancellation.Cancel();
                nook.Signal.Set();
            }
        });

        try
        {
            while (!streamCancellation.IsCancellationRequested)
            {
                var wait = nook.Signal.WaitAsync();
                lock (gate)
                {
                    if (!childMarked && nook.Reader.HasCompleted)
                    {
                        sender.MarkChildExited(nook.Reader.ExitCode);
                        childMarked = true;
                    }
                    sender.PumpAvailable();
                }
                if (sender.Ended || sender.Faulted)
                    break;
                try
                {
                    await wait.WaitAsync(streamCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            _logger.SubscribeEnded(
                parameters.NookId,
                sender.Ended,
                sender.Faulted);
            streamCancellation.Cancel();
            try
            {
                await creditLoop.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.SubscribeCreditLoopClosed(parameters.NookId, ex.Message);
            }
        }
    }

    private static ControlResponse Fail(string id, string code, string message)
    {
        return new ControlResponse(
            id,
            false,
            null,
            new ControlError(code, message));
    }

    private static ValueTask WriteResponseAsync(
        FrameConnection connection,
        ControlResponse response,
        CancellationToken cancellationToken)
    {
        return connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(response),
            cancellationToken);
    }
}
