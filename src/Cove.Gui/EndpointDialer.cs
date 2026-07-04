using System.IO.Pipes;
using System.Net.Sockets;
using Cove.Platform;
using Cove.Protocol;

namespace Cove.Gui;

public static class EndpointDialer
{
    public static async Task<Stream> DialAsync(string channel, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
        {
            var pipe = new NamedPipeClientStream(".", $"cove-{channel}", PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(ProtocolConstants.ReadinessTimeoutMs, ct);
            return pipe;
        }
        var dd = CoveDataDir.Resolve(ParseChannel(channel));
        var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await sock.ConnectAsync(new UnixDomainSocketEndPoint(dd.SocketPath), ct);
        return new NetworkStream(sock, ownsSocket: true);
    }

    private static CoveChannel ParseChannel(string channel) => channel switch
    {
        "beta" => CoveChannel.Beta,
        "dev" => CoveChannel.Dev,
        _ => CoveChannel.Stable,
    };
}
