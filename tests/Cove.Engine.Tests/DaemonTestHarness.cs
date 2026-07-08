using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class DaemonTestHarness : IAsyncDisposable
{
    private DaemonPaths _paths = null!;
    private Task _run = null!;
    private string _parent = "";
    private string? _prev;
    private CancellationTokenSource _cts = new();
    private DaemonHost? _host;
    private Cove.Tasks.Dispatch.DispatchSaga? _dispatchSaga;
    private Cove.Tasks.Dispatch.ResumeSaga? _resumeSaga;

    public IControlEndpoint Endpoint { get; private set; } = null!;
    public void SetSagas(Cove.Tasks.Dispatch.DispatchSaga? dispatchSaga, Cove.Tasks.Dispatch.ResumeSaga? resumeSaga)
    {
        _dispatchSaga = dispatchSaga;
        _resumeSaga = resumeSaga;
        _host?.SetSagas(dispatchSaga, resumeSaga);
    }
    public Task Run => _run;
    public string DataDir => _parent;
    public static async Task<DaemonTestHarness> StartAsync(Cove.Tasks.Dispatch.DispatchSaga? dispatchSaga = null, Cove.Tasks.Dispatch.ResumeSaga? resumeSaga = null, string? dataDir = null)
    {
        var h = new DaemonTestHarness();
        h._dispatchSaga = dispatchSaga;
        h._resumeSaga = resumeSaga;
        h._parent = dataDir ?? Path.Combine(Path.GetTempPath(), "cove-daemon-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        h._prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        Environment.SetEnvironmentVariable("COVE_DATA_DIR", h._parent);

        CoveDataDir dd = CoveDataDir.Resolve(CoveChannel.Dev);
        h._paths = new DaemonPaths(dd);
        h.Endpoint = ControlEndpointFactory.FromSocketPath(dd.SocketPath);
        var host = new DaemonHost(h._paths, h.Endpoint, exitWhenIdle: false, dispatchSaga, resumeSaga);
        h._host = host;
        h._run = Task.Run(() => host.RunAsync(h._cts.Token));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < 10)
        {
            if (h.Endpoint.TryProbe(100))
                return h;
            await Task.Delay(20);
        }
        throw new TimeoutException("daemon did not become connectable");
    }

    public async Task<FrameConnection> ConnectAsync(string clientKind)
    {
        Stream s = await Endpoint.ConnectAsync(5000, CancellationToken.None);
        var conn = new FrameConnection(s);
        JsonElement hp = JsonSerializer.SerializeToElement(
            new HelloParams(1, clientKind, "0.1.0", "dev"), CoveJsonContext.Default.HelloParams);
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
        try { await _run.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        _host = new DaemonHost(_paths, Endpoint, exitWhenIdle: false, _dispatchSaga, _resumeSaga);
        var runHost = _host;
        var runCts = _cts;
        _run = Task.Run(() => runHost.RunAsync(runCts.Token));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < 10)
        {
            if (Endpoint.TryProbe(100))
                return;
            await Task.Delay(20);
        }
        throw new TimeoutException("daemon did not restart");
    }


    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _run.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
        Environment.SetEnvironmentVariable("COVE_DATA_DIR", _prev);
        try { if (Directory.Exists(_parent)) Directory.Delete(_parent, recursive: true); } catch { }
        _cts.Dispose();
    }
}
