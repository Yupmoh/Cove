using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Cove.Engine.Agents;
using Cove.Engine.Browser;
using Cove.Engine.Hooks;
using Cove.Engine.Pty;
using Cove.Engine.Restart;
using Cove.Engine.Sessions;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("Cove.Engine.Tests")]

namespace Cove.Engine.Daemon;

internal sealed class HandoffTransportTestHooks
{
    public Action? ListenerReady { get; init; }
    public Action? ServeCleanupStarting { get; init; }
    public Action? ServeCleanupCompleted { get; init; }
    public Action? DisposeStarted { get; init; }
    public Action? SocketPathReleased { get; init; }
    public Action? ListenerReleased { get; init; }
    public Action? CancellationReleased { get; init; }
}

internal sealed class HandoffTransport : IAsyncDisposable
{
    private readonly DaemonPaths _paths;
    private readonly NookRegistry _nooks;
    private readonly BrowserNookManager _browser;
    private readonly HookEventRouter _hookRouter;
    private readonly AgentMessageRouter _agentRouter;
    private readonly SessionResumeOrchestrator _sessions;
    private readonly EngineEventRouter _events;
    private readonly ILogger _logger;
    private readonly Action _requestShutdown;
    private readonly HandoffTransportTestHooks? _testHooks;
    private readonly object _ownershipLock = new();
    private HandoffOwnership? _ownership;
    private long _generation;
    private bool _disposed;

    private sealed class HandoffOwnership(
        long generation,
        string socketPath)
    {
        public long Generation { get; } = generation;
        public string SocketPath { get; } = socketPath;
        public TaskCompletionSource Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public Socket? Listener { get; set; }
        public CancellationTokenSource? Cancellation { get; set; }
        public Task? ServeTask { get; set; }
        public bool CleanupStarted { get; set; }
        public bool CancellationRequested { get; set; }
    }

    public HandoffTransport(
        DaemonPaths paths,
        NookRegistry nooks,
        BrowserNookManager browser,
        HookEventRouter hookRouter,
        AgentMessageRouter agentRouter,
        SessionResumeOrchestrator sessions,
        EngineEventRouter events,
        ILogger logger,
        Action requestShutdown,
        HandoffTransportTestHooks? testHooks = null)
    {
        _paths = paths;
        _nooks = nooks;
        _browser = browser;
        _hookRouter = hookRouter;
        _agentRouter = agentRouter;
        _sessions = sessions;
        _events = events;
        _logger = logger;
        _requestShutdown = requestShutdown;
        _testHooks = testHooks;
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
        _browser.Restore(takeover.BrowserNooks);
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
        if (takeover.Items.Count > 0 || takeover.BrowserNooks.Count > 0)
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
        var socketPath = Path.Combine(
            _paths.DataDir.IpcDir,
            "handoff.sock");
        HandoffOwnership ownership;
        lock (_ownershipLock)
        {
            if (_disposed)
                return Fail(requestId, "disposed", "handoff transport is disposed");
            if (_ownership is not null)
                return Fail(requestId, "conflict", "handoff already in progress");
            ownership = new HandoffOwnership(
                ++_generation,
                socketPath);
            _ownership = ownership;
        }

        var items = new List<HandoffExportItem>();
        HandoffBrowserNookDto[] browserNooks;
        Socket? listener = null;
        CancellationTokenSource? serveCts = null;
        var ownsSocketPath = false;
        try
        {
            browserNooks = _browser.Snapshot();
            var exported = _nooks.ExportForHandoff();
            items.Capacity = exported.Count;
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
            File.Delete(socketPath);
            listener = new Socket(
                AddressFamily.Unix,
                SocketType.Stream,
                ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            ownsSocketPath = true;
            listener.Listen(1);
            _testHooks?.ListenerReady?.Invoke();
            serveCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(15));
        }
        catch (Exception ex)
        {
            var cleanupFailure = CleanupUnpublished(
                ownership,
                listener,
                serveCts,
                ownsSocketPath,
                items);
            var failure = CombineFailures(ex, cleanupFailure);
            DaemonLog.Write(_paths, "handoff begin failed: " + failure);
            return Fail(
                requestId,
                "io_error",
                "handoff socket setup failed: " + ex.Message);
        }

        var published = false;
        lock (_ownershipLock)
        {
            if (!_disposed && ReferenceEquals(_ownership, ownership))
            {
                ownership.Listener = listener;
                ownership.Cancellation = serveCts;
                ownership.ServeTask = ServeAsync(
                    ownership,
                    listener!,
                    items,
                    serveCts!.Token);
                published = true;
            }
        }
        if (!published)
        {
            var cleanupFailure = CleanupUnpublished(
                ownership,
                listener,
                serveCts,
                ownsSocketPath,
                items);
            if (cleanupFailure is not null)
            {
                DaemonLog.Write(
                    _paths,
                    "handoff disposal unwind failed: " + cleanupFailure);
            }
            return Fail(
                requestId,
                "disposed",
                "handoff transport is disposed");
        }
        DaemonLog.Write(
            _paths,
            $"handoff begin: exporting {items.Count} nooks via {socketPath}");
        return new ControlResponse(
            requestId,
            true,
            JsonSerializer.SerializeToElement(
                new HandoffBeginResult(
                    items.Count,
                    socketPath,
                    browserNooks),
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

    private async Task ServeAsync(
        HandoffOwnership ownership,
        Socket listener,
        IReadOnlyList<HandoffExportItem> items,
        CancellationToken cancellationToken)
    {
        Exception? primaryFailure = null;
        List<Exception>? cleanupFailures = null;
        try
        {
            using var accepted = await listener
                .AcceptAsync(cancellationToken)
                .ConfigureAwait(false);
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
            primaryFailure = ex;
            DaemonLog.Write(
                _paths,
                "handoff aborted: "
                    + ex.Message
                    + "; re-adopting exported sessions");
            try
            {
                ReadoptExports(items);
            }
            catch (Exception cleanupException)
            {
                (cleanupFailures ??= []).Add(cleanupException);
            }
        }
        finally
        {
            lock (_ownershipLock)
                ownership.CleanupStarted = true;
            CaptureCleanupFailure(
                cleanupFailures ??= [],
                _testHooks?.ServeCleanupStarting);
            try
            {
                if (OwnsSocketPath(ownership))
                {
                    File.Delete(ownership.SocketPath);
                    _testHooks?.SocketPathReleased?.Invoke();
                }
            }
            catch (Exception ex)
            {
                cleanupFailures.Add(ex);
            }
            try
            {
                listener.Dispose();
                _testHooks?.ListenerReleased?.Invoke();
            }
            catch (Exception ex)
            {
                cleanupFailures.Add(ex);
            }
            try
            {
                ownership.Cancellation!.Dispose();
                _testHooks?.CancellationReleased?.Invoke();
            }
            catch (Exception ex)
            {
                cleanupFailures.Add(ex);
            }
            lock (_ownershipLock)
            {
                if (ReferenceEquals(_ownership, ownership))
                    _ownership = null;
            }
            CaptureCleanupFailure(
                cleanupFailures,
                _testHooks?.ServeCleanupCompleted);
            ownership.Completion.TrySetResult();
            if (cleanupFailures.Count > 0)
            {
                DaemonLog.Write(
                    _paths,
                    "handoff cleanup failed: "
                        + CombineFailures(
                            primaryFailure,
                            new AggregateException(cleanupFailures)));
            }
        }
    }

    private Exception? CleanupUnpublished(
        HandoffOwnership ownership,
        Socket? listener,
        CancellationTokenSource? cancellation,
        bool ownsSocketPath,
        IReadOnlyList<HandoffExportItem> items)
    {
        List<Exception>? failures = null;
        if (items.Count > 0)
        {
            try
            {
                ReadoptExports(items);
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }
        if (ownsSocketPath && OwnsSocketPath(ownership))
        {
            try
            {
                File.Delete(ownership.SocketPath);
                _testHooks?.SocketPathReleased?.Invoke();
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }
        try
        {
            listener?.Dispose();
            if (listener is not null)
                _testHooks?.ListenerReleased?.Invoke();
        }
        catch (Exception ex)
        {
            (failures ??= []).Add(ex);
        }
        try
        {
            cancellation?.Dispose();
            if (cancellation is not null)
                _testHooks?.CancellationReleased?.Invoke();
        }
        catch (Exception ex)
        {
            (failures ??= []).Add(ex);
        }
        lock (_ownershipLock)
        {
            if (ReferenceEquals(_ownership, ownership))
                _ownership = null;
        }
        ownership.Completion.TrySetResult();
        return failures is { Count: > 0 }
            ? new AggregateException(failures)
            : null;
    }

    private bool OwnsSocketPath(HandoffOwnership ownership)
    {
        lock (_ownershipLock)
        {
            return ReferenceEquals(_ownership, ownership)
                && _ownership.Generation == ownership.Generation;
        }
    }

    private static void CaptureCleanupFailure(
        List<Exception> failures,
        Action? cleanup)
    {
        if (cleanup is null)
            return;
        try
        {
            cleanup();
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
    }

    private static Exception CombineFailures(
        Exception? primary,
        Exception? cleanup)
    {
        if (primary is null)
            return cleanup ?? new InvalidOperationException(
                "handoff failed without an exception");
        if (cleanup is null)
            return primary;
        return new AggregateException(primary, cleanup);
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
        HandoffOwnership? ownership;
        var startedDisposal = false;
        lock (_ownershipLock)
        {
            if (!_disposed)
            {
                _disposed = true;
                startedDisposal = true;
            }
            ownership = _ownership;
            if (startedDisposal
                && ownership is
                {
                    CleanupStarted: false,
                    Cancellation: not null,
                    CancellationRequested: false,
                })
            {
                ownership.CancellationRequested = true;
                ownership.Cancellation.Cancel();
            }
        }
        if (startedDisposal)
            _testHooks?.DisposeStarted?.Invoke();
        if (ownership is not null)
            await ownership.Completion.Task.ConfigureAwait(false);
    }
}
