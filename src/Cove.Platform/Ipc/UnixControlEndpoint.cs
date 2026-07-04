using System.Net.Sockets;

namespace Cove.Platform.Ipc;

public sealed class UnixControlEndpoint : IControlEndpoint
{
    private readonly string _path;

    public UnixControlEndpoint(string path) => _path = path;

    public string Address => _path;

    public IControlListener Bind()
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Bind(new UnixDomainSocketEndPoint(_path));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            socket.Listen(128);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
        return new UnixControlListener(socket);
    }

    public async ValueTask<Stream> ConnectAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(_path), cts.Token).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
        return new NetworkStream(socket, ownsSocket: true);
    }

    public bool TryProbe(int timeoutMs)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            socket.ConnectAsync(new UnixDomainSocketEndPoint(_path), cts.Token).AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch
        {
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
    private readonly Socket _listener;

    public UnixControlListener(Socket listener) => _listener = listener;

    public async ValueTask<Stream> AcceptAsync(CancellationToken cancellationToken)
    {
        Socket client = await _listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
        return new NetworkStream(client, ownsSocket: true);
    }

    public ValueTask DisposeAsync()
    {
        _listener.Dispose();
        return ValueTask.CompletedTask;
    }
}
