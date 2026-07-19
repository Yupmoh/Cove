using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Cove.Testing;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class DaemonTestHarness : IAsyncDisposable
{
    public sealed class LifecycleOptions
    {
        public TimeSpan ReadinessTimeout { get; init; } =
            TimeSpan.FromSeconds(10);
        public TimeSpan GracefulStopTimeout { get; init; } =
            TimeSpan.FromSeconds(5);
        public TimeSpan HardStopTimeout { get; init; } =
            TimeSpan.FromSeconds(5);
        public Func<IControlEndpoint, bool>? ReadinessProbe { get; init; }
        public Action<CancellationTokenSource>? RequestGracefulStop { get; init; }
        public Action<DaemonHost>? RequestHardStop { get; init; }
    }

    private DaemonPaths _paths = null!;
    private Task<int> _run = Task.FromResult(0);
    private string _parent = "";
    private bool _ownsDataDir;
    private ProcessEnvironmentScope? _environment;
    private CancellationTokenSource _cts = new();
    private DaemonHost? _host;
    private Cove.Tasks.Dispatch.DispatchSaga? _dispatchSaga;
    private Cove.Tasks.Dispatch.ResumeSaga? _resumeSaga;
    private LifecycleOptions _lifecycle = new();
    private readonly object _disposeGate = new();
    private Task? _disposeTask;

    public IControlEndpoint Endpoint { get; private set; } = null!;
    public void SetSagas(Cove.Tasks.Dispatch.DispatchSaga? dispatchSaga, Cove.Tasks.Dispatch.ResumeSaga? resumeSaga)
    {
        _dispatchSaga = dispatchSaga;
        _resumeSaga = resumeSaga;
        _host?.SetSagas(dispatchSaga, resumeSaga);
    }
    public Task Run => _run;
    public string DataDir => _parent;
    public static async Task<DaemonTestHarness> StartAsync(
        Cove.Tasks.Dispatch.DispatchSaga? dispatchSaga = null,
        Cove.Tasks.Dispatch.ResumeSaga? resumeSaga = null,
        string? dataDir = null,
        LifecycleOptions? lifecycle = null)
    {
        var h = new DaemonTestHarness();
        h._lifecycle = lifecycle ?? new LifecycleOptions();
        h._dispatchSaga = dispatchSaga;
        h._resumeSaga = resumeSaga;
        h._ownsDataDir = dataDir is null;
        h._parent = dataDir ?? TestDirectory.Create(
            "cd-",
            OperatingSystem.IsWindows() ? null : "/tmp");

        try
        {
            h._environment = await ProcessEnvironmentScope.SetAsync("COVE_DATA_DIR", h._parent);
            CoveDataDir dd = CoveDataDir.Resolve(CoveChannel.Dev);
            h._paths = new DaemonPaths(dd);
            h.Endpoint = ControlEndpointFactory.FromSocketPath(dd.SocketPath);
            var host = new DaemonHost(h._paths, h.Endpoint, exitWhenIdle: false, dispatchSaga, resumeSaga);
            h._host = host;
            h._run = Task.Run(() => host.RunAsync(h._cts.Token));

            await AsyncTest.EventuallyAsync(() =>
            {
                if (h._run.IsFaulted)
                    h._run.GetAwaiter().GetResult();
                if (h._run.IsCompleted)
                {
                    var logPath = Path.Combine(h._paths.DataDir.LogsDir, $"{h._paths.Channel}.log");
                    var log = File.Exists(logPath) ? File.ReadAllText(logPath) : "no daemon log";
                    throw new InvalidOperationException($"daemon exited with code {h._run.GetAwaiter().GetResult()} before becoming connectable: {log}");
                }
                return h._lifecycle.ReadinessProbe?.Invoke(h.Endpoint)
                    ?? h.Endpoint.TryProbe(100);
            }, h._lifecycle.ReadinessTimeout, "daemon did not become connectable");
            return h;
        }
        catch (Exception startupException)
        {
            try
            {
                await h.DisposeAsync();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(startupException, cleanupException);
            }
            throw;
        }
    }

    public string ControlTokenPath => _paths.ControlTokenPath;

    public string? ReadControlToken()
    {
        try { return File.Exists(ControlTokenPath) ? File.ReadAllText(ControlTokenPath).Trim() : null; }
        catch (IOException) { return null; }
    }

    public async Task<FrameConnection> ConnectAsync(string clientKind)
    {
        Stream s = await Endpoint.ConnectAsync(5000, CancellationToken.None);
        var conn = new FrameConnection(s);
        var controlToken = ReadControlToken();
        JsonElement hp = JsonSerializer.SerializeToElement(
            new HelloParams(1, clientKind, "0.1.0", "dev", ControlToken: controlToken), CoveJsonContext.Default.HelloParams);
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("1", "cove://sys/hello", hp)), CancellationToken.None);
        Frame resp = (await conn.ReadFrameAsync(CancellationToken.None))!.Value;
        ControlResponse r = ControlCodec.DecodeResponse(resp.Payload);
        Assert.True(r.Ok);
        return conn;
    }
    public async Task RestartAsync()
    {
        _cts.Cancel();
        try
        {
            await _run.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        _host = new DaemonHost(_paths, Endpoint, exitWhenIdle: false, _dispatchSaga, _resumeSaga);
        var runHost = _host;
        var runCts = _cts;
        _run = Task.Run(() => runHost.RunAsync(runCts.Token));
        await AsyncTest.EventuallyAsync(() =>
        {
            if (_run.IsFaulted)
                _run.GetAwaiter().GetResult();
            return Endpoint.TryProbe(100);
        }, TimeSpan.FromSeconds(10), "daemon did not restart");
    }


    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
            return new ValueTask(_disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        var failures = new System.Collections.Generic.List<Exception>();
        try
        {
            (_lifecycle.RequestGracefulStop
                ?? (static cts => cts.Cancel()))(_cts);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }

        try
        {
            await _run.WaitAsync(_lifecycle.GracefulStopTimeout);
        }
        catch (TimeoutException ex)
        {
            failures.Add(new TimeoutException(
                $"daemon did not terminate within {_lifecycle.GracefulStopTimeout} after graceful stop",
                ex));
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }

        if (!_run.IsCompleted)
        {
            try
            {
                var host = _host
                    ?? throw new InvalidOperationException(
                        "daemon host is unavailable for hard stop");
                (_lifecycle.RequestHardStop
                    ?? (static current => current.RequestHardStop()))(host);
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }

            try
            {
                await _run.WaitAsync(_lifecycle.HardStopTimeout);
            }
            catch (TimeoutException ex)
            {
                failures.Add(new TimeoutException(
                    $"daemon did not terminate within {_lifecycle.HardStopTimeout} after hard stop",
                    ex));
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }

        if (!_run.IsCompleted)
        {
            throw new AggregateException(
                "daemon test harness could not confirm daemon termination; process-global state and owned resources were retained",
                failures);
        }

        if (_environment is not null)
        {
            try
            {
                await _environment.DisposeAsync();
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }
        if (_ownsDataDir)
        {
            try
            {
                TestDirectory.Delete(_parent);
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }
        try
        {
            _cts.Dispose();
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
        if (failures.Count > 0)
            throw new AggregateException("daemon test harness cleanup failed", failures);
    }
}
