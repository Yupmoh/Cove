using System.Diagnostics;
using System.Text.Json;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Protocol;

namespace Cove.Engine.Daemon;

public sealed class DaemonStartTimeoutException : Exception
{
    public DaemonStartTimeoutException(string message) : base(message) { }
}

public sealed class DaemonConnector
{
    private readonly DaemonPaths _paths;
    private readonly IControlEndpoint _endpoint;

    public DaemonConnector(DaemonPaths paths, IControlEndpoint endpoint)
    {
        _paths = paths;
        _endpoint = endpoint;
    }

    public async Task<FrameConnection> ConnectOrSpawnAsync(string clientKind, CancellationToken cancellationToken)
    {
        FrameConnection? conn = await TryConnectAndHelloAsync(clientKind, cancellationToken).ConfigureAwait(false);
        if (conn is not null)
            return conn;

        using SpawnLock spawnLock = SpawnLock.Acquire(_paths.SpawnLockPath);

        conn = await TryConnectAndHelloAsync(clientKind, cancellationToken).ConfigureAwait(false);
        if (conn is not null)
            return conn;

        SpawnDaemon();

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ProtocolConstants.ReadinessTimeoutMs)
        {
            await Task.Delay(ProtocolConstants.SpawnPollMs, cancellationToken).ConfigureAwait(false);
            conn = await TryConnectAndHelloAsync(clientKind, cancellationToken).ConfigureAwait(false);
            if (conn is not null)
                return conn;
        }
        throw new DaemonStartTimeoutException(
            $"daemon did not become connectable within {ProtocolConstants.ReadinessTimeoutMs} ms on channel {_paths.Channel}");
    }

    public async Task<FrameConnection?> TryConnectAndHelloAsync(string clientKind, CancellationToken cancellationToken)
    {
        Stream stream;
        try
        {
            stream = await _endpoint.ConnectAsync(ProtocolConstants.ReadinessTimeoutMs, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }

        var conn = new FrameConnection(stream);
        try
        {
            var nookId = Environment.GetEnvironmentVariable("COVE_NOOK_ID");
            var nookToken = Environment.GetEnvironmentVariable("COVE_NOOK_TOKEN");
            var hasNookCredential =
                !string.IsNullOrEmpty(nookId)
                && !string.IsNullOrEmpty(nookToken);
            var controlToken = hasNookCredential
                ? null
                : ControlCredential.Read(_paths.DataDir);
            JsonElement hp = JsonSerializer.SerializeToElement(
                new HelloParams(
                    ProtocolConstants.SemanticProtocolVersion,
                    clientKind,
                    CoveBuild.InformationalVersion,
                    _paths.Channel,
                    hasNookCredential ? nookId : null,
                    hasNookCredential ? nookToken : null,
                    controlToken),
                CoveJsonContext.Default.HelloParams);
            await conn.WriteFrameAsync(FrameType.Request, 0,
                ControlCodec.Encode(new ControlRequest("1", "cove://sys/hello", hp)), cancellationToken).ConfigureAwait(false);
            Frame? resp = await conn.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            if (resp is null)
            {
                await conn.DisposeAsync().ConfigureAwait(false);
                return null;
            }
            ControlResponse r = ControlCodec.DecodeResponse(resp.Value.Payload);
            if (!r.Ok)
            {
                await conn.DisposeAsync().ConfigureAwait(false);
                return null;
            }
            return conn;
        }
        catch
        {
            await conn.DisposeAsync().ConfigureAwait(false);
            return null;
        }
    }

    private void SpawnDaemon()
    {
        string exe = Environment.ProcessPath
            ?? throw new DaemonStartTimeoutException("Environment.ProcessPath is null; cannot self-spawn the daemon");
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("daemon");
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--channel");
        psi.ArgumentList.Add(_paths.Channel);
        Process.Start(psi);
    }
}
