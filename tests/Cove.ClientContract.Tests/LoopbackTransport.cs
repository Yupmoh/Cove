using System.Net;
using System.Net.Sockets;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Testing;

namespace Cove.ClientContract.Tests;

internal sealed class LoopbackTransport : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly TcpClient _server;

    private LoopbackTransport(TcpClient client, TcpClient server)
    {
        _client = client;
        _server = server;
        ClientStream = client.GetStream();
        ServerStream = server.GetStream();
    }

    public Stream ClientStream { get; }
    public Stream ServerStream { get; }

    public static async Task<LoopbackTransport> CreateAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var client = new TcpClient();
            var connect = client.ConnectAsync(endpoint.Address, endpoint.Port, cancellationToken);
            var server = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            await connect.ConfigureAwait(false);
            return new LoopbackTransport(client, server);
        }
        finally
        {
            listener.Stop();
        }
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _server.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed class CliControlTransport : IAsyncDisposable
{
    private readonly IControlListener _listener;

    private CliControlTransport(string root, IControlListener listener)
    {
        Root = root;
        _listener = listener;
    }

    public string Root { get; }

    public static CliControlTransport Create()
    {
        var root = TestDirectory.Create(
            "cc-",
            OperatingSystem.IsWindows() ? null : "/tmp");
        var dataDirectory = CoveDataDir.ForRoot(CoveChannel.Dev, root);
        CoveTree.Ensure(dataDirectory);
        var endpoint = ControlEndpointFactory.FromSocketPath(dataDirectory.SocketPath);
        return new CliControlTransport(root, endpoint.Bind());
    }

    public ValueTask<Stream> AcceptAsync(CancellationToken cancellationToken) =>
        _listener.AcceptAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _listener.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            TestDirectory.Delete(Root);
        }
    }
}
