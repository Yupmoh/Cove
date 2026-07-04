using System.Runtime.InteropServices;
using System.Text.Json;
using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Protocol;
using Cove.Cli;
using Cove.Generated;

string[] cliArgs = args;
using (var cliLoggerFactory = Cove.Platform.CoveLog.CreateConsoleLoggerFactory())
    cliLoggerFactory.CreateLogger("cove").Invoked(string.Join(' ', cliArgs));
CoveChannel channel = ParseChannel(cliArgs);
CoveDataDir dataDir = CoveDataDir.Resolve(channel);
var paths = new DaemonPaths(dataDir);
IControlEndpoint endpoint = ControlEndpointFactory.FromSocketPath(dataDir.SocketPath);

if (cliArgs.Length >= 2 && cliArgs[0] == "daemon")
{
    return cliArgs[1] switch
    {
        "run" => await RunDaemonAsync(paths, endpoint, HasFlag(cliArgs, "--exit-when-idle")),
        "status" => await StatusAsync(paths, endpoint),
        "stop" => await StopAsync(paths, endpoint),
        _ => Unknown(cliArgs[1]),
    };
}

string? matchedVerb = null;
{
    var positional = new System.Collections.Generic.List<string>();
    for (int i = 0; i < cliArgs.Length; i++)
    {
        if (cliArgs[i] == "--channel" && i + 1 < cliArgs.Length) { i++; continue; }
        if (cliArgs[i].StartsWith("--", System.StringComparison.Ordinal)) continue;
        positional.Add(cliArgs[i]);
    }
    for (int take = System.Math.Min(positional.Count, 3); take >= 1 && matchedVerb is null; take--)
    {
        var candidate = string.Join(' ', positional.GetRange(0, take));
        if (CoveCommandRegistry.Keys.Contains(candidate)) matchedVerb = candidate;
    }
}
if (matchedVerb is not null)
{
    var context = new CommandContext(paths, endpoint, System.Console.Out);
    var handler = (System.Func<CommandContext, System.Threading.Tasks.Task<int>>)CoveCommandRegistry.Handlers[matchedVerb];
    return await handler(context);
}

return await DefaultConnectAndFocusAsync(paths, endpoint);

static CoveChannel ParseChannel(string[] a)
{
    for (int i = 0; i < a.Length - 1; i++)
        if (a[i] == "--channel")
            return a[i + 1] switch
            {
                "stable" => CoveChannel.Stable,
                "beta" => CoveChannel.Beta,
                "dev" => CoveChannel.Dev,
                _ => CoveChannel.Stable,
            };
    return CoveChannel.Stable;
}

static bool HasFlag(string[] a, string flag)
{
    foreach (string s in a)
        if (s == flag)
            return true;
    return false;
}

static int Unknown(string sub)
{
    Console.Error.WriteLine($"unknown daemon subcommand: {sub}");
    return 2;
}

static async Task<int> RunDaemonAsync(DaemonPaths paths, IControlEndpoint endpoint, bool exitWhenIdle)
{
    using var cts = new CancellationTokenSource();
    using PosixSignalRegistration sigInt = PosixSignalRegistration.Create(PosixSignal.SIGINT, _ => cts.Cancel());
    using PosixSignalRegistration sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => cts.Cancel());
    var host = new DaemonHost(paths, endpoint, exitWhenIdle);
    return await host.RunAsync(cts.Token);
}

static async Task<int> StatusAsync(DaemonPaths paths, IControlEndpoint endpoint)
{
    var connector = new DaemonConnector(paths, endpoint);
    FrameConnection? conn = await connector.TryConnectAndHelloAsync("cli", CancellationToken.None);
    if (conn is null)
    {
        Console.Error.WriteLine("no daemon running");
        return 1;
    }
    await using (conn)
    {
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://sys/daemon.status")), CancellationToken.None);
        Frame? resp = await conn.ReadFrameAsync(CancellationToken.None);
        if (resp is not { } f)
        {
            Console.Error.WriteLine("no response");
            return 1;
        }
        ControlResponse r = ControlCodec.DecodeResponse(f.Payload);
        Console.WriteLine(r.Data is { } d ? d.GetRawText() : "{}");
        return 0;
    }
}

static async Task<int> StopAsync(DaemonPaths paths, IControlEndpoint endpoint)
{
    var connector = new DaemonConnector(paths, endpoint);
    FrameConnection? conn = await connector.TryConnectAndHelloAsync("cli", CancellationToken.None);
    if (conn is null)
    {
        Console.WriteLine("no daemon running");
        return 0;
    }
    await using (conn)
    {
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://sys/daemon.stop")), CancellationToken.None);
        await conn.ReadFrameAsync(CancellationToken.None);
        Console.WriteLine("stopping");
        return 0;
    }
}

static async Task<int> DefaultConnectAndFocusAsync(DaemonPaths paths, IControlEndpoint endpoint)
{
    var connector = new DaemonConnector(paths, endpoint);
    FrameConnection conn = await connector.ConnectOrSpawnAsync("cli", CancellationToken.None);
    await using (conn)
    {
        await conn.WriteFrameAsync(FrameType.Request, 0,
            ControlCodec.Encode(new ControlRequest("2", "cove://commands/window.focus")), CancellationToken.None);
        Frame? resp = await conn.ReadFrameAsync(CancellationToken.None);
        if (resp is { } f)
        {
            ControlResponse r = ControlCodec.DecodeResponse(f.Payload);
            Console.WriteLine(r.Data is { } d ? d.GetRawText() : "{}");
        }
        return 0;
    }
}
