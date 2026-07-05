using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine.Pty;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Daemon;

public sealed class DaemonHost
{
    private readonly DaemonPaths _paths;
    private readonly IControlEndpoint _endpoint;
    private readonly bool _exitWhenIdle;
    private readonly string _engineVersion = CoveBuild.InformationalVersion;
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _guiLock = new();
    private readonly List<FrameConnection> _guiConnections = new();

    private int _totalConnections;
    private int _activeConnections;
    private long _lastActivityTicks;

    private IPtyHost? _ptyHost;
    private PaneRegistry? _panes;
    private Cove.Engine.Layout.LayoutService? _layout;

    public DaemonHost(DaemonPaths paths, IControlEndpoint endpoint, bool exitWhenIdle)
    {
        _paths = paths;
        _endpoint = endpoint;
        _exitWhenIdle = exitWhenIdle;
    }

    public async Task<int> RunAsync(CancellationToken externalCancellation)
    {
        CoveTree.Ensure(_paths.DataDir);
        using var loggerFactory = Cove.Platform.CoveLog.CreateEngineLoggerFactory(_paths.DataDir.LogsDir, _paths.Channel);
        var logger = loggerFactory.CreateLogger<DaemonHost>();

        _ptyHost = PtyHostFactory.Create(logger);
        var probedPath = Cove.Platform.LoginShellPath.Probe(logger);
        var dataDir = _paths.DataDir.Root;
        var cliPath = System.IO.Path.Combine(dataDir, "bin", "cove");
        var spawnEnv = new SpawnEnvironment(probedPath, dataDir, cliPath, "default");
        var shellDir = ShellIntegration.Install(dataDir);
        _panes = new PaneRegistry(_ptyHost, logger, spawnEnv, shellDir);
        _layout = new Cove.Engine.Layout.LayoutService();
        var wsDir = System.IO.Path.Combine(dataDir, "workspaces", "default");
        var (savedLayout, sessions) = Cove.Engine.Layout.WorkspacePersistence.Load(wsDir, logger);
        if (savedLayout is { } sl)
        {
            foreach (var room in sl.Rooms)
                foreach (var leaf in Cove.Engine.Layout.MosaicOps.Leaves(room.LayoutTree))
                    if (sessions.TryGetValue(leaf.PaneId, out var d))
                    {
                        try { _panes!.RespawnAs(d.PaneId, d.Command, d.Args, d.Cwd, 80, 24); }
                        catch (System.Exception ex) { logger.LogWarning(ex, "respawn on restore failed for {PaneId}", d.PaneId); }
                    }
            _layout!.LoadSnapshot(sl);
        }
        _layout!.OnChanged = () =>
        {
            try { Cove.Engine.Layout.WorkspacePersistence.Save(_layout.ToSnapshot("default", "default", System.Environment.CurrentDirectory), _panes!.Descriptors(), wsDir); }
            catch (System.Exception ex) { logger.LogWarning(ex, "workspace persist failed"); }
        };
        SingleInstanceGuard? guard = SingleInstanceGuard.TryAcquire(_paths.PidFilePath);
        if (guard is null)
        {
            DaemonLog.Write(_paths, "daemon already running on channel " + _paths.Channel);
            return 0;
        }

        if (!OperatingSystem.IsWindows() && File.Exists(_paths.SocketPath))
        {
            if (_endpoint.TryProbe(250))
            {
                DaemonLog.Write(_paths, "stale_reclaim_conflict on channel " + _paths.Channel);
                guard.Dispose();
                return 1;
            }
            try { File.Delete(_paths.SocketPath); }
            catch (Exception ex) { DaemonLog.Write(_paths, "stale unlink failed: " + ex.Message); }
        }

        IControlListener listener;
        try
        {
            listener = _endpoint.Bind();
        }
        catch (Exception ex)
        {
            DaemonLog.Write(_paths, "bind failed (already running?): " + ex.Message);
            guard.Dispose();
            return 0;
        }

        guard.WritePid(Environment.ProcessId);
        _lastActivityTicks = DateTimeOffset.UtcNow.Ticks;
        DaemonLog.Write(_paths, $"daemon up pid={Environment.ProcessId} channel={_paths.Channel} addr={_endpoint.Address}");
        logger.DaemonStarted(System.Environment.ProcessId, _paths.Channel);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(externalCancellation, _shutdown.Token);
        Task? idle = _exitWhenIdle ? Task.Run(() => IdleMonitorAsync(linked.Token)) : null;

        try
        {
            await AcceptLoopAsync(listener, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await listener.DisposeAsync().ConfigureAwait(false);
        _panes?.Dispose();
        if (!OperatingSystem.IsWindows())
        {
            try { File.Delete(_paths.SocketPath); } catch { }
        }
        PidFile.Delete(_paths.PidFilePath);
        guard.Dispose();
        if (idle is not null)
        {
            try { await idle.ConfigureAwait(false); } catch { }
        }
        logger.DaemonStopping(_paths.Channel);
        DaemonLog.Write(_paths, "daemon down");
        return 0;
    }

    private async Task AcceptLoopAsync(IControlListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Stream stream;
            try
            {
                stream = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                DaemonLog.Write(_paths, "accept error: " + ex.Message);
                break;
            }
            _ = HandleConnectionAsync(stream, cancellationToken);
        }
    }

    private async Task HandleConnectionAsync(Stream stream, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _totalConnections);
        MarkActive(1);
        var conn = new FrameConnection(stream);
        var state = new ConnState();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Frame? maybe = await conn.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
                if (maybe is null)
                    break;
                Frame f = maybe.Value;
                if (f.Header.Type == FrameType.Credit)
                    continue;
                if (f.Header.Type != FrameType.Request)
                {
                    await WriteErrorFrameAsync(conn, "malformed_frame", "control connection expects Request frames", null, cancellationToken).ConfigureAwait(false);
                    break;
                }
                ControlRequest req = ControlCodec.DecodeRequest(f.Payload);
                bool stop = await DispatchAsync(conn, stream, state, req, cancellationToken).ConfigureAwait(false);
                if (stop)
                    break;
            }
        }
        catch (ProtocolException pex)
        {
            try { await WriteErrorFrameAsync(conn, pex.Code, pex.Message, null, cancellationToken).ConfigureAwait(false); }
            catch { }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DaemonLog.Write(_paths, "connection error: " + ex.Message);
        }
        finally
        {
            if (state.IsGui)
                UnregisterGui(conn);
            try { await conn.DisposeAsync().ConfigureAwait(false); } catch { }
            MarkActive(-1);
        }
    }

    private async Task<bool> DispatchAsync(FrameConnection conn, Stream stream, ConnState state, ControlRequest req, CancellationToken cancellationToken)
    {
        if (req.Uri == "cove://sys/hello")
        {
            if (req.Params is not JsonElement helloEl)
            {
                await WriteResponseAsync(conn, Fail(req.Id, "invalid_params", "hello params required"), cancellationToken).ConfigureAwait(false);
                return false;
            }
            HelloParams? hp = helloEl.Deserialize(CoveJsonContext.Default.HelloParams);
            if (hp is null)
            {
                await WriteResponseAsync(conn, Fail(req.Id, "invalid_params", "hello params malformed"), cancellationToken).ConfigureAwait(false);
                return false;
            }
            if (hp.ProtocolVersion != ProtocolConstants.SemanticProtocolVersion)
            {
                await WriteErrorFrameAsync(conn, "version_mismatch", $"protocol {hp.ProtocolVersion} unsupported", null, cancellationToken).ConfigureAwait(false);
                return true;
            }
            state.HelloDone = true;
            if (hp.ClientKind == "gui")
            {
                state.IsGui = true;
                RegisterGui(conn);
            }
            var hr = new HelloResult(ProtocolConstants.SemanticProtocolVersion, _engineVersion, Environment.ProcessId, _paths.Channel);
            await WriteResponseAsync(conn, new ControlResponse(req.Id, true, ToElement(hr, CoveJsonContext.Default.HelloResult)), cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (!state.HelloDone)
        {
            await WriteResponseAsync(conn, Fail(req.Id, "not_ready", "sys/hello required before other requests"), cancellationToken).ConfigureAwait(false);
            return false;
        }

        ControlResponse? generated = await Cove.Engine.EngineCommandRouter.RouteAsync(req, _panes, _layout, cancellationToken).ConfigureAwait(false);
        if (generated is not null)
        {
            await WriteResponseAsync(conn, generated, cancellationToken).ConfigureAwait(false);
            return false;
        }

        switch (req.Uri)
        {
            case "cove://sys/ping":
                await WriteResponseAsync(conn, new ControlResponse(req.Id, true, Parse("{\"pong\":true}")), cancellationToken).ConfigureAwait(false);
                return false;

            case "cove://sys/daemon.status":
                {
                    var status = new DaemonStatusResult(
                        Environment.ProcessId,
                        _paths.Channel,
                        _engineVersion,
                        Volatile.Read(ref _totalConnections),
                        0,
                        (long)(DateTimeOffset.UtcNow - _startedAtUtc).TotalSeconds);
                    await WriteResponseAsync(conn, new ControlResponse(req.Id, true, ToElement(status, CoveJsonContext.Default.DaemonStatusResult)), cancellationToken).ConfigureAwait(false);
                    return false;
                }

            case "cove://sys/daemon.stop":
                await WriteResponseAsync(conn, new ControlResponse(req.Id, true, Parse("{\"stopping\":true}")), cancellationToken).ConfigureAwait(false);
                _shutdown.Cancel();
                return true;

            case "cove://commands/window.focus":
                {
                    bool focused = TryForwardFocus(cancellationToken);
                    JsonElement data = focused
                        ? Parse("{\"focused\":true}")
                        : Parse("{\"focused\":false,\"reason\":\"no_render_client\"}");
                    await WriteResponseAsync(conn, new ControlResponse(req.Id, true, data), cancellationToken).ConfigureAwait(false);
                    return false;
                }

            case "cove://commands/pane.subscribe":
                await StreamPaneAsync(conn, stream, req, cancellationToken).ConfigureAwait(false);
                return true;

            default:
                await WriteResponseAsync(conn, Fail(req.Id, "not_found", $"unknown command {req.Uri}"), cancellationToken).ConfigureAwait(false);
                return false;
        }
    }

    private async Task StreamPaneAsync(FrameConnection conn, Stream stream, ControlRequest req, CancellationToken cancellationToken)
    {
        if (_panes is null || req.Params is not JsonElement el
            || el.Deserialize(CoveJsonContext.Default.SubscribeParams) is not { } sp)
        {
            await WriteResponseAsync(conn, Fail(req.Id, "invalid_params", "subscribe params required"), cancellationToken).ConfigureAwait(false);
            return;
        }
        if (!_panes.TryGet(sp.PaneId, out PaneSession pane))
        {
            await WriteResponseAsync(conn, Fail(req.Id, "not_found", $"unknown pane {sp.PaneId}"), cancellationToken).ConfigureAwait(false);
            return;
        }

        const ulong streamId = 1;
        long head = pane.Ring.Head;
        long tail = pane.Ring.Tail;
        long baseOffset = Math.Clamp((long)sp.SinceOffset, tail, head);
        var subResult = new SubscribeResult(streamId, (ulong)baseOffset, ProtocolConstants.FlowWindow);
        await WriteResponseAsync(conn, new ControlResponse(req.Id, true, ToElement(subResult, CoveJsonContext.Default.SubscribeResult)), cancellationToken).ConfigureAwait(false);

        var sink = new SocketByteStreamSink(stream);
        var sender = new PtyStreamSender(streamId, pane.Session.SessionId, pane.Ring, baseOffset, sink);
        var gate = new object();
        bool childMarked = false;

        using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task creditLoop = Task.Run(async () =>
        {
            try
            {
                while (!streamCts.IsCancellationRequested)
                {
                    Frame? maybe = await conn.ReadFrameAsync(streamCts.Token).ConfigureAwait(false);
                    if (maybe is null)
                        break;
                    Frame f = maybe.Value;
                    if (f.Header.Type == FrameType.Credit && f.Payload.Length >= 8)
                    {
                        ulong ack = BinaryPrimitives.ReadUInt64LittleEndian(f.Payload);
                        lock (gate)
                            sender.OnCredit(ack);
                    }
                    pane.Signal.Set();
                }
            }
            catch
            {
            }
            finally
            {
                streamCts.Cancel();
                pane.Signal.Set();
            }
        });

        try
        {
            while (!streamCts.IsCancellationRequested)
            {
                Task wait = pane.Signal.WaitAsync();
                lock (gate)
                {
                    if (!childMarked && pane.Reader.HasCompleted)
                    {
                        sender.MarkChildExited(pane.Reader.ExitCode);
                        childMarked = true;
                    }
                    sender.PumpAvailable();
                }
                if (sender.Ended || sender.Faulted)
                    break;
                try { await wait.WaitAsync(streamCts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            streamCts.Cancel();
            try { await creditLoop.ConfigureAwait(false); } catch { }
        }
    }

    private bool TryForwardFocus(CancellationToken cancellationToken)
    {
        FrameConnection? gui;
        lock (_guiLock)
            gui = _guiConnections.Count > 0 ? _guiConnections[0] : null;
        if (gui is null)
            return false;
        _ = gui.WriteFrameAsync(FrameType.Event, 0, ControlCodec.Encode(new ControlEvent("window.focus", Parse("{}"))), cancellationToken);
        return true;
    }

    private async Task IdleMonitorAsync(CancellationToken cancellationToken)
    {
        long idleTicks = TimeSpan.FromSeconds(ProtocolConstants.IdleExitSeconds).Ticks;
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await Task.Delay(1000, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            if (Volatile.Read(ref _activeConnections) > 0)
                continue;
            if (DateTimeOffset.UtcNow.Ticks - Volatile.Read(ref _lastActivityTicks) >= idleTicks)
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
        Volatile.Write(ref _lastActivityTicks, DateTimeOffset.UtcNow.Ticks);
    }

    private void RegisterGui(FrameConnection conn)
    {
        lock (_guiLock)
            _guiConnections.Add(conn);
    }

    private void UnregisterGui(FrameConnection conn)
    {
        lock (_guiLock)
            _guiConnections.Remove(conn);
    }

    private static ControlResponse Fail(string id, string code, string message) =>
        new(id, false, null, new ControlError(code, message));

    private static JsonElement ToElement<T>(T value, JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.SerializeToElement(value, typeInfo);

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static ValueTask WriteResponseAsync(FrameConnection conn, ControlResponse resp, CancellationToken cancellationToken) =>
        conn.WriteFrameAsync(FrameType.Response, 0, ControlCodec.Encode(resp), cancellationToken);

    private static ValueTask WriteErrorFrameAsync(FrameConnection conn, string code, string message, ulong? streamId, CancellationToken cancellationToken) =>
        conn.WriteFrameAsync(FrameType.Error, 0, ControlCodec.Encode(new ControlErrorFrame(code, message, streamId)), cancellationToken);

    private sealed class ConnState
    {
        public bool HelloDone;
        public bool IsGui;
    }
}
