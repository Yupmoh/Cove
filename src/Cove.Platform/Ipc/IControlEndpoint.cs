namespace Cove.Platform.Ipc;

public interface IControlListener : IAsyncDisposable
{
    ValueTask<Stream> AcceptAsync(CancellationToken cancellationToken);
}

public interface IControlEndpoint
{
    string Address { get; }

    IControlListener Bind();

    ValueTask<Stream> ConnectAsync(int timeoutMs, CancellationToken cancellationToken);

    bool TryProbe(int timeoutMs);
}
