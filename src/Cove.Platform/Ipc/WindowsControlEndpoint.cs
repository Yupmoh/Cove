using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Platform.Ipc;

[SupportedOSPlatform("windows")]
public sealed class WindowsControlEndpoint : IControlEndpoint
{
    private const string Transport = "named-pipe";
    private readonly string _pipeName;
    private readonly ILogger _logger;

    public WindowsControlEndpoint(string pipeName, ILogger? logger = null)
    {
        _pipeName = pipeName;
        _logger = logger ?? NullLogger.Instance;
    }

    public string Address => $@"\\.\pipe\{_pipeName}";

    public IControlListener Bind()
    {
        _logger.EndpointBindBegin(Transport, Address);
        try
        {
            NamedPipeServerStream first = CreateServerStream(firstInstance: true);
            _logger.EndpointBound(Transport, Address);
            return new WindowsControlListener(first, CreateServerStream, Address, _logger);
        }
        catch (Exception ex)
        {
            _logger.EndpointBindFailed(Transport, Address, ex.Message);
            throw;
        }
    }

    private NamedPipeServerStream CreateServerStream(bool firstInstance)
    {
        var security = new PipeSecurity();
        SecurityIdentifier user = WindowsIdentity.GetCurrent().User!;
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));

        PipeOptions options = PipeOptions.Asynchronous;
        if (firstInstance)
            options |= PipeOptions.FirstPipeInstance;

        NamedPipeServerStream stream = NamedPipeServerStreamAcl.Create(
            _pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            options,
            0,
            0,
            security);
        _logger.EndpointServerInstanceCreated(Address, firstInstance);
        return stream;
    }

    public async ValueTask<Stream> ConnectAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        _logger.EndpointConnectBegin(Transport, Address, timeoutMs);
        var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await client.ConnectAsync(timeoutMs, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.EndpointConnectFailed(Transport, Address, ex.Message);
            client.Dispose();
            throw;
        }
        _logger.EndpointConnected(Transport, Address);
        return client;
    }

    public bool TryProbe(int timeoutMs)
    {
        _logger.EndpointProbeBegin(Transport, Address, timeoutMs);
        var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            client.Connect(timeoutMs);
            _logger.EndpointProbeReachable(Transport, Address);
            return true;
        }
        catch (Exception ex)
        {
            _logger.EndpointProbeUnreachable(Transport, Address, ex.Message);
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
    private const string Transport = "named-pipe";
    private readonly Func<bool, NamedPipeServerStream> _factory;
    private readonly string _address;
    private readonly ILogger _logger;
    private NamedPipeServerStream _pending;

    public WindowsControlListener(NamedPipeServerStream first, Func<bool, NamedPipeServerStream> factory, string address, ILogger logger)
    {
        _pending = first;
        _factory = factory;
        _address = address;
        _logger = logger;
    }

    public async ValueTask<Stream> AcceptAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            NamedPipeServerStream server = _pending;
            _logger.EndpointAcceptBegin(Transport, _address);
            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                _logger.EndpointAcceptRetry(_address, ex.Message);
                server.Dispose();
                _pending = _factory(false);
                continue;
            }
            _pending = _factory(false);
            _logger.EndpointAccepted(Transport, _address);
            return server;
        }
    }

    public ValueTask DisposeAsync()
    {
        _pending.Dispose();
        return ValueTask.CompletedTask;
    }
}
