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
    private readonly CancellationTokenSource _cts = new();

    public IControlEndpoint Endpoint { get; private set; } = null!;
    public Task Run => _run;

    public static async Task<DaemonTestHarness> StartAsync()
    {
        var h = new DaemonTestHarness();
        h._parent = Path.Combine(Path.GetTempPath(), "cove-daemon-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        h._prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        Environment.SetEnvironmentVariable("COVE_DATA_DIR", h._parent);

        CoveDataDir dd = CoveDataDir.Resolve(CoveChannel.Dev);
        h._paths = new DaemonPaths(dd);
        h.Endpoint = ControlEndpointFactory.FromSocketPath(dd.SocketPath);
        var host = new DaemonHost(h._paths, h.Endpoint, exitWhenIdle: false);
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

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _run.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
        Environment.SetEnvironmentVariable("COVE_DATA_DIR", _prev);
        try { if (Directory.Exists(_parent)) Directory.Delete(_parent, recursive: true); } catch { }
        _cts.Dispose();
    }
}
