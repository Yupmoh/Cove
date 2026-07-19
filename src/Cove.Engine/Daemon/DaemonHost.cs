using Cove.Platform.Ipc;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Daemon;

public sealed class DaemonHost
{
    private readonly DaemonPaths _paths;
    private readonly IControlEndpoint _endpoint;
    private readonly bool _exitWhenIdle;
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly DaemonCommandSagas _sagas;
    private int _totalConnections;
    private int _activeConnections;
    private long _lastActivityTicks;

    public DaemonHost(
        DaemonPaths paths,
        IControlEndpoint endpoint,
        bool exitWhenIdle,
        Cove.Tasks.Dispatch.DispatchSaga? dispatchSaga = null,
        Cove.Tasks.Dispatch.ResumeSaga? resumeSaga = null)
    {
        _paths = paths;
        _endpoint = endpoint;
        _exitWhenIdle = exitWhenIdle;
        _sagas = new DaemonCommandSagas(dispatchSaga, resumeSaga);
    }

    public void SetSagas(
        Cove.Tasks.Dispatch.DispatchSaga? dispatchSaga,
        Cove.Tasks.Dispatch.ResumeSaga? resumeSaga)
    {
        _sagas.Set(dispatchSaga, resumeSaga);
    }

    public void RequestHardStop()
    {
        _shutdown.Cancel();
    }

    public async Task<int> RunAsync(
        CancellationToken externalCancellation)
    {
        Directory.CreateDirectory(_paths.DataDir.Root);
        Directory.CreateDirectory(_paths.DataDir.IpcDir);
        Directory.CreateDirectory(_paths.DataDir.LogsDir);
        var minimumLevel = ResolveMinimumLogLevel();
        var logPath = Path.Combine(
            _paths.DataDir.LogsDir,
            $"{_paths.Channel}.log");
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            builder.AddZLoggerConsole();
            builder.AddZLoggerFile(logPath);
        });
        var logger = loggerFactory.CreateLogger<DaemonHost>();
        logger.LogLevelResolved(
            minimumLevel.ToString(),
            Environment.GetEnvironmentVariable("COVE_LOG_LEVEL")
                ?? "");

        var takeover = await HandoffTransport.TryTakeOverAsync(
            _paths,
            _endpoint,
            logger,
            externalCancellation).ConfigureAwait(false);
        var leaseAttempt = DaemonLease.TryAcquireOwnership(
            _paths,
            _endpoint,
            retry: takeover is not null,
            logger);
        if (!leaseAttempt.Acquired)
            return leaseAttempt.ExitCode;

        await using var lease = leaseAttempt.Lease!;
        var events = new EngineEventRouter(_shutdown.Token);
        await using var runtime = await EngineRuntime.CreateAsync(
            _paths,
            logger,
            events,
            _startedAtUtc,
            _shutdown.Token).ConfigureAwait(false);
        await using var handoff = new HandoffTransport(
            _paths,
            runtime.Nooks,
            runtime.HookRouter,
            runtime.AgentRouter,
            runtime.Sessions,
            events,
            logger,
            _shutdown.Cancel);
        await runtime.InitializeAsync(
            handoff,
            takeover).ConfigureAwait(false);

        var publishResult = lease.TryPublishControlEndpoint();
        if (!publishResult.Published)
            return publishResult.ExitCode;
        await runtime.PublishReadyAsync().ConfigureAwait(false);

        Volatile.Write(
            ref _lastActivityTicks,
            DateTimeOffset.UtcNow.Ticks);
        DaemonLog.Write(
            _paths,
            $"daemon up pid={Environment.ProcessId} channel={_paths.Channel} addr={_endpoint.Address}");
        logger.DaemonStarted(
            Environment.ProcessId,
            _paths.Channel);

        using var linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                externalCancellation,
                _shutdown.Token);
        var idle = _exitWhenIdle
            ? Task.Run(() => IdleMonitorAsync(linked.Token))
            : null;

        try
        {
            await AcceptLoopAsync(
                lease.Listener,
                runtime,
                handoff,
                lease.ControlToken,
                logger,
                linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await lease.CloseListenerAsync().ConfigureAwait(false);
        await runtime.ShutdownAsync().ConfigureAwait(false);
        if (idle is not null)
        {
            try
            {
                await idle.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                logger.IdleMonitorFailed(ex.Message);
            }
        }
        await handoff.DisposeAsync().ConfigureAwait(false);
        await runtime.DisposeAsync().ConfigureAwait(false);
        await lease.DisposeAsync().ConfigureAwait(false);
        logger.DaemonStopping(_paths.Channel);
        DaemonLog.Write(_paths, "daemon down");
        return 0;
    }

    private async Task AcceptLoopAsync(
        IControlListener listener,
        EngineRuntime runtime,
        HandoffTransport handoff,
        string controlToken,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var sessionGate = new object();
        var sessions = new HashSet<Task>();
        while (!cancellationToken.IsCancellationRequested)
        {
            Stream stream;
            try
            {
                stream = await listener.AcceptAsync(
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                DaemonLog.Write(
                    _paths,
                    "accept error: " + ex.Message);
                _shutdown.Cancel();
                break;
            }
            var session = RunSessionAsync(
                stream,
                runtime,
                handoff,
                controlToken,
                logger,
                cancellationToken);
            lock (sessionGate)
                sessions.Add(session);
            _ = ObserveSessionAsync(
                session,
                sessions,
                sessionGate,
                logger);
        }

        Task[] remaining;
        lock (sessionGate)
            remaining = sessions.ToArray();
        if (remaining.Length > 0)
        {
            try
            {
                await Task.WhenAll(remaining).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.ControlSessionFaulted(ex.Message);
            }
        }
    }

    private async Task RunSessionAsync(
        Stream stream,
        EngineRuntime runtime,
        HandoffTransport handoff,
        string controlToken,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _totalConnections);
        MarkActive(1);
        try
        {
            var session = new ControlSession(
                _paths,
                runtime,
                handoff,
                _sagas,
                controlToken,
                logger,
                _shutdown.Cancel,
                () => Volatile.Read(ref _totalConnections));
            await session.RunAsync(
                stream,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            MarkActive(-1);
        }
    }

    private static async Task ObserveSessionAsync(
        Task session,
        HashSet<Task> sessions,
        object sessionGate,
        ILogger logger)
    {
        try
        {
            await session.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.ControlSessionFaulted(ex.Message);
        }
        finally
        {
            lock (sessionGate)
                sessions.Remove(session);
        }
    }

    private async Task IdleMonitorAsync(
        CancellationToken cancellationToken)
    {
        var idleTicks = TimeSpan.FromSeconds(
            Cove.Protocol.ProtocolConstants.IdleExitSeconds).Ticks;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    1000,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            if (Volatile.Read(ref _activeConnections) > 0)
                continue;
            if (DateTimeOffset.UtcNow.Ticks
                - Volatile.Read(ref _lastActivityTicks)
                >= idleTicks)
            {
                DaemonLog.Write(_paths, "idle-exit");
                _shutdown.Cancel();
                break;
            }
        }
    }

    private void MarkActive(int delta)
    {
        Interlocked.Add(ref _activeConnections, delta);
        Volatile.Write(
            ref _lastActivityTicks,
            DateTimeOffset.UtcNow.Ticks);
    }

    private static LogLevel ResolveMinimumLogLevel()
    {
        var raw = Environment.GetEnvironmentVariable(
            "COVE_LOG_LEVEL");
        if (string.IsNullOrWhiteSpace(raw))
            return LogLevel.Information;
        return raw.Trim().ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" or "information" => LogLevel.Information,
            "warn" or "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            _ => LogLevel.Information,
        };
    }
}
