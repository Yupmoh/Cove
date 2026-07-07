using Cove.Engine.Daemon;
using Cove.Platform.Ipc;
using Cove.Protocol;

namespace Cove.Cli;

public sealed class CommandContext
{
    public CommandContext(DaemonPaths paths, IControlEndpoint endpoint, TextWriter stdout, TextWriter? stderr = null)
    {
        Paths = paths;
        Endpoint = endpoint;
        Stdout = stdout;
        Stderr = stderr ?? System.Console.Error;
    }

    public DaemonPaths Paths { get; }
    public IControlEndpoint Endpoint { get; }
    public TextWriter Stdout { get; }
    public TextWriter Stderr { get; }

    public async Task<int> RouteCoreAsync(string uri)
    {
        var connector = new DaemonConnector(Paths, Endpoint);
        FrameConnection conn = await connector.ConnectOrSpawnAsync("cli", CancellationToken.None);
        await using (conn)
        {
            await conn.WriteFrameAsync(FrameType.Request, 0,
                ControlCodec.Encode(new ControlRequest("1", uri)), CancellationToken.None);
            Frame? resp = await conn.ReadFrameAsync(CancellationToken.None);
            if (resp is not { } f)
            {
                Stdout.WriteLine("error: no_response");
                return 1;
            }
            ControlResponse r = ControlCodec.DecodeResponse(f.Payload);
            if (!r.Ok)
            {
                Stdout.WriteLine($"error: {r.Error?.Code ?? "unknown"}");
                return 1;
            }
            Stdout.WriteLine(r.Data is { } d ? d.GetRawText() : "{}");
            return 0;
        }
    }
}
