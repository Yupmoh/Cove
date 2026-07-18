using System.Net.Sockets;
using System.Text.Json;
using Cove.Engine.Agents;
using Cove.Engine.Hooks;
using Cove.Engine.Pty;
using Cove.Engine.Restart;
using Cove.Engine.Sessions;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Daemon;

internal sealed class HandoffTransport : IAsyncDisposable
{
    private readonly DaemonPaths _paths;
    private readonly NookRegistry _nooks;
    private readonly HookEventRouter _hookRouter;
    private readonly AgentMessageRouter _agentRouter;
    private readonly SessionResumeOrchestrator _sessions;
    private readonly EngineEventRouter _events;
    private readonly ILogger _logger;
    private readonly Action _requestShutdown;
    private readonly object _serveLock = new();
    private Socket? _listener;
    private Task? _serveTask;
    private int _handoffStarted;
    private int _disposed;

    public HandoffTransport(
        DaemonPaths paths,
        NookRegistry nooks,
        HookEventRouter hookRouter,
        AgentMessageRouter agentRouter,
        SessionResumeOrchestrator sessions,
        EngineEventRouter events,
        ILogger logger,
        Action requestShutdown)
    {
        _paths = paths;
        _nooks = nooks;
        _hookRouter = hookRouter;
        _agentRouter = agentRouter;
        _sessions = sessions;
        _events = events;
        _logger = logger;
        _requestShutdown = requestShutdown;
    }

    public static Task<HandoffTakeover?> TryTakeOverAsync(
        DaemonPaths paths,
        IControlEndpoint endpoint,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows()
            || Environment.GetEnvironmentVariable("COVE_HANDOFF") != "1")
        {
            return Task.FromResult<HandoffTakeover?>(null);
        }
        return HandoffClient.TryTakeOverAsync(
            paths,
            endpoint,
            logger,
            cancellationToken);
    }

    public HashSet<string> AdoptTakenOverNooks(HandoffTakeover takeover)
    {
        var adopted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in takeover.Items)
        {
            var info = _nooks.Adopt(item.Record, item.Fd, item.Ring);
            if (info is null)
            {
                Cove.Platform.Pty.Unix.UnixFdChannel.CloseFd(item.Fd);
                _logger.HandoffAdoptionFellBack(item.Record.NookId);
                continue;
            }
            if (item.Record.Adapter is { } adapter)
            {
                _agentRouter.Register(
                    info.NookId,
                    adapter,
                    item.Record.AgentName,
                    status: item.Record.HookStatus ?? "idle");
                _sessions.Register(
                    info.NookId,
                    adapter,
                    item.Record.SessionId);
                _hookRouter.Seed(
                    info.NookId,
                    adapter,
                    item.Record.SessionId,
                    item.Record.HookStatus);
            }
            adopted.Add(info.NookId);
            _logger.HandoffSuccessorAdopted(
                info.NookId,
                item.Record.Adapter ?? "");
        }
        if (takeover.Items.Count > 0)
        {
            _events.Broadcast(
                "state.changed",
                new StateChangedEvent("cove://events/handoff.adopted"),
                CoveJsonContext.Default.StateChangedEvent);
        }
        return adopted;
    }

    public ControlResponse Begin(string requestId)
    {
        if (OperatingSystem.IsWindows())
            return Fail(requestId, "unsupported", "handoff requires a unix host");
        if (Interlocked.Exchange(ref _handoffStarted, 1) != 0)
            return Fail(requestId, "conflict", "handoff already in progress");

        var exported = _nooks.ExportForHandoff();
        var items = new List<HandoffExportItem>(exported.Count);
        foreach (var item in exported)
        {
            var state = _hookRouter.GetNookState(item.Record.NookId);
            items.Add(
                item with
                {
                    Record = item.Record with
                    {
                        SessionId = state?.SessionId,
                        HookStatus = state?.Status,
                    },
                });
        }

        var socketPath = Path.Combine(_paths.DataDir.IpcDir, "handoff.sock");
        Socket? listener = null;
        try
        {
            try
            {
                File.Delete(socketPath);
            }
            catch (IOException)
            {
            }
            listener = new Socket(
                AddressFamily.Unix,
                SocketType.Stream,
                ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            listener.Listen(1);
        }
        catch (Exception ex)
        {
            listener?.Dispose();
            DaemonLog.Write(
                _paths,
                "handoff begin failed: "
                    + ex.Message
                    + "; re-adopting exported sessions");
            ReadoptExports(items);
            Volatile.Write(ref _handoffStarted, 0);
            return Fail(
                requestId,
                "io_error",
                "handoff socket setup failed: " + ex.Message);
        }

        lock (_serveLock)
        {
            _listener = listener;
            _serveTask = Task.Run(() => Serve(listener, socketPath, items));
        }
        DaemonLog.Write(
            _paths,
            $"handoff begin: exporting {items.Count} nooks via {socketPath}");
        return new ControlResponse(
            requestId,
            true,
            JsonSerializer.SerializeToElement(
                new HandoffBeginResult(items.Count, socketPath),
                CoveJsonContext.Default.HandoffBeginResult));
    }

    private void ReadoptExports(IReadOnlyList<HandoffExportItem> items)
    {
        foreach (var item in items)
        {
            var restored = _nooks.Adopt(
                item.Record,
                item.MasterFd,
                item.RingTail);
            if (restored is null)
                Cove.Platform.Pty.Unix.UnixFdChannel.CloseFd(item.MasterFd);
        }
    }

    private void Serve(
        Socket listener,
        string socketPath,
        IReadOnlyList<HandoffExportItem> items)
    {
        try
        {
            if (!listener.Poll(
                    15_000_000,
                    SelectMode.SelectRead))
            {
                throw new TimeoutException(
                    "no successor connected within 15s");
            }
            using var accepted = listener.Accept();
            accepted.SendTimeout = 10_000;
            accepted.ReceiveTimeout = 10_000;
            var socketFd = (int)accepted.Handle;
            foreach (var item in items)
            {
                HandoffWire.WriteRecord(
                    socketFd,
                    item.Record,
                    item.MasterFd,
                    item.RingTail);
            }
            accepted.ReceiveTimeout = 10_000;
            var acknowledgement = new byte[1];
            var received = accepted.Receive(acknowledgement);
            if (received != 1 || acknowledgement[0] != (byte)'K')
                throw new IOException("handoff ack missing");
            foreach (var item in items)
                Cove.Platform.Pty.Unix.UnixFdChannel.CloseFd(item.MasterFd);
            DaemonLog.Write(
                _paths,
                $"handoff complete: {items.Count} nooks transferred, shutting down");
            _requestShutdown();
        }
        catch (Exception ex)
        {
            DaemonLog.Write(
                _paths,
                "handoff aborted: "
                    + ex.Message
                    + "; re-adopting exported sessions");
            ReadoptExports(items);
            Volatile.Write(ref _handoffStarted, 0);
        }
        finally
        {
            listener.Dispose();
            lock (_serveLock)
            {
                if (ReferenceEquals(_listener, listener))
                    _listener = null;
            }
            try
            {
                File.Delete(socketPath);
            }
            catch (IOException)
            {
            }
        }
    }

    private static ControlResponse Fail(
        string id,
        string code,
        string message)
    {
        return new ControlResponse(
            id,
            false,
            null,
            new ControlError(code, message));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Socket? listener;
        Task? serveTask;
        lock (_serveLock)
        {
            listener = _listener;
            serveTask = _serveTask;
        }
        listener?.Dispose();
        if (serveTask is not null)
        {
            try
            {
                await serveTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DaemonLog.Write(
                    _paths,
                    "handoff shutdown failed: " + ex.Message);
            }
        }
    }
}
