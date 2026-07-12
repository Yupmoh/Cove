using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Platform.Ipc;

public sealed class UnixControlEndpoint : IControlEndpoint
{
    private const string Transport = "unix-socket";
    private readonly string _path;
    private readonly ILogger _logger;

    public UnixControlEndpoint(string path, ILogger? logger = null)
    {
        _path = path;
        _logger = logger ?? NullLogger.Instance;
    }

    public string Address => _path;

    public IControlListener Bind()
    {
        _logger.EndpointBindBegin(Transport, _path);
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Bind(new UnixDomainSocketEndPoint(_path));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            socket.Listen(128);
        }
        catch (Exception ex)
        {
            _logger.EndpointBindFailed(Transport, _path, ex.Message);
            socket.Dispose();
            throw;
        }
        _logger.EndpointBound(Transport, _path);
        return new UnixControlListener(socket, _path, _logger);
    }

    public async ValueTask<Stream> ConnectAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        _logger.EndpointConnectBegin(Transport, _path, timeoutMs);
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(_path), cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.EndpointConnectFailed(Transport, _path, ex.Message);
            socket.Dispose();
            throw;
        }
        _logger.EndpointConnected(Transport, _path);
        return new NetworkStream(socket, ownsSocket: true);
    }

    public bool TryProbe(int timeoutMs)
    {
        _logger.EndpointProbeBegin(Transport, _path, timeoutMs);
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            socket.ConnectAsync(new UnixDomainSocketEndPoint(_path), cts.Token).AsTask().GetAwaiter().GetResult();
            _logger.EndpointProbeReachable(Transport, _path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.EndpointProbeUnreachable(Transport, _path, ex.Message);
            return false;
        }
        finally
        {
            socket.Dispose();
        }
    }
}

internal sealed class UnixControlListener : IControlListener
{
    private const string Transport = "unix-socket";
    private readonly Socket _listener;
    private readonly string _path;
    private readonly ILogger _logger;

    public UnixControlListener(Socket listener, string path, ILogger logger)
    {
        _listener = listener;
        _path = path;
        _logger = logger;
    }

    public async ValueTask<Stream> AcceptAsync(CancellationToken cancellationToken)
    {
        _logger.EndpointAcceptBegin(Transport, _path);
        Socket client = await _listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
        _logger.EndpointAccepted(Transport, _path);
        return new NetworkStream(client, ownsSocket: true);
    }

    public ValueTask DisposeAsync()
    {
        _listener.Dispose();
        return ValueTask.CompletedTask;
    }
}
