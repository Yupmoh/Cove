using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Protocol;

namespace Cove.Gui;

public static class EndpointDialer
{
    public static async Task<Stream> DialAsync(string channel, CancellationToken ct)
    {
        var dd = CoveDataDir.Resolve(ParseChannel(channel));
        var endpoint = ControlEndpointFactory.FromSocketPath(dd.SocketPath);
        return await endpoint.ConnectAsync(ProtocolConstants.ReadinessTimeoutMs, ct);
    }

    private static CoveChannel ParseChannel(string channel) => channel switch
    {
        "beta" => CoveChannel.Beta,
        "dev" => CoveChannel.Dev,
        _ => CoveChannel.Stable,
    };
}
