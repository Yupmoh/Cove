using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Cove.Platform.Ipc;

[SupportedOSPlatform("windows")]
public sealed class WindowsControlEndpoint : IControlEndpoint
{
    private readonly string _pipeName;

    public WindowsControlEndpoint(string pipeName) => _pipeName = pipeName;

    public string Address => $@"\\.\pipe\{_pipeName}";

    public IControlListener Bind()
    {
        NamedPipeServerStream first = CreateServerStream(firstInstance: true);
        return new WindowsControlListener(first, CreateServerStream);
    }

    private NamedPipeServerStream CreateServerStream(bool firstInstance)
    {
        var security = new PipeSecurity();
        SecurityIdentifier user = WindowsIdentity.GetCurrent().User!;
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));

        PipeOptions options = PipeOptions.Asynchronous;
        if (firstInstance)
            options |= PipeOptions.FirstPipeInstance;

        return NamedPipeServerStreamAcl.Create(
            _pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            options,
            0,
            0,
            security);
    }

    public async ValueTask<Stream> ConnectAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await client.ConnectAsync(timeoutMs, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            throw;
        }
        return client;
    }

    public bool TryProbe(int timeoutMs)
    {
        var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            client.Connect(timeoutMs);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            client.Dispose();
        }
    }
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsControlListener : IControlListener
{
    private readonly Func<bool, NamedPipeServerStream> _factory;
    private NamedPipeServerStream _pending;

    public WindowsControlListener(NamedPipeServerStream first, Func<bool, NamedPipeServerStream> factory)
    {
        _pending = first;
        _factory = factory;
    }

    public async ValueTask<Stream> AcceptAsync(CancellationToken cancellationToken)
    {
        NamedPipeServerStream server = _pending;
        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        _pending = _factory(false);
        return server;
    }

    public ValueTask DisposeAsync()
    {
        _pending.Dispose();
        return ValueTask.CompletedTask;
    }
}
