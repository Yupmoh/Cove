using System.Text.Json;
using Cove.Engine.Daemon;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Protocol;

namespace Cove.Cli;

public sealed class CommandContext
{
    public CommandContext(DaemonPaths paths, IControlEndpoint endpoint, TextWriter stdout, TextWriter? stderr = null, string[]? args = null)
    {
        Paths = paths;
        Endpoint = endpoint;
        Stdout = stdout;
        Stderr = stderr ?? System.Console.Error;
        Args = args ?? System.Array.Empty<string>();
        Channel = ParseChannel(Args);
        IsJson = HasFlag(Args, "--json") || System.Environment.GetEnvironmentVariable("COVE_JSON") == "1";
        Filter = FlagValue(Args, "--filter");
        Source = FlagValue(Args, "--source") ?? "user:cli";
    }

    public DaemonPaths Paths { get; }
    public IControlEndpoint Endpoint { get; }
    public TextWriter Stdout { get; }
    public TextWriter Stderr { get; }
    public string[] Args { get; }
    public CoveChannel Channel { get; }
    public bool IsJson { get; }
    public string? Filter { get; }
    public string Source { get; }

    public void Render(JsonElement data)
    {
        var filtered = ApplyFilter(data);
        Stdout.WriteLine(filtered.GetRawText());
    }

    public void RenderStatus(string message)
    {
        if (!IsJson)
            Stderr.WriteLine(message);
    }

    public async Task<int> RouteCoreAsync(string uri)
        => await RouteCoreWithParamsAsync(uri, null);

    public async Task<int> RouteCoreWithParamsAsync(string uri, string? paramsJson)
    {
        System.Text.Json.JsonElement? parsedParams = null;
        if (paramsJson is not null)
        {
            try
            {
                parsedParams = JsonDocument.Parse(paramsJson).RootElement.Clone();
            }
            catch (System.Text.Json.JsonException)
            {
                Stderr.WriteLine("error: invalid_params");
                Stderr.WriteLine("usage: --params '<json>'");
                return 1;
            }
        }
        var connector = new DaemonConnector(Paths, Endpoint);
        FrameConnection conn = await connector.ConnectOrSpawnAsync("cli", System.Threading.CancellationToken.None);
        await using (conn)
        {
            await conn.WriteFrameAsync(FrameType.Request, 0,
                ControlCodec.Encode(new ControlRequest("1", uri, Params: parsedParams, Source: Source)), System.Threading.CancellationToken.None);
            Frame? resp = await conn.ReadFrameAsync(System.Threading.CancellationToken.None);
            if (resp is not { } f)
            {
                Stderr.WriteLine("error: no_response");
                return 1;
            }
            ControlResponse r = ControlCodec.DecodeResponse(f.Payload);
            if (!r.Ok)
            {
                Stderr.WriteLine($"error: {r.Error?.Code ?? "unknown"}");
                return 1;
            }
            if (r.Data is { } d)
                Render(d);
            else
                Stdout.WriteLine("{}");
            return 0;
        }
    }

    private JsonElement ApplyFilter(JsonElement data)
    {
        if (string.IsNullOrEmpty(Filter) || data.ValueKind != JsonValueKind.Array)
            return data;
        var eq = Filter.IndexOf('=');
        if (eq < 0)
            return data;
        var col = Filter[..eq];
        var val = Filter[(eq + 1)..];
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var item in data.EnumerateArray())
            {
                bool match = false;
                if (item.TryGetProperty(col, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.String)
                        match = prop.GetString()?.Contains(val, System.StringComparison.OrdinalIgnoreCase) == true;
                    else
                        match = prop.GetRawText().Contains(val, System.StringComparison.OrdinalIgnoreCase);
                }
                if (match)
                    item.WriteTo(writer);
            }
            writer.WriteEndArray();
            writer.Flush();
        }
        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }

    private static CoveChannel ParseChannel(string[] args)
    {
        var ch = FlagValue(args, "--channel");
        return ch?.ToLowerInvariant() switch
        {
            "beta" => CoveChannel.Beta,
            "dev" => CoveChannel.Dev,
            _ => CoveChannel.Stable,
        };
    }

    private static bool HasFlag(string[] args, string flag)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == flag)
                return true;
        return false;
    }

    public static string[] SliceVerbArgs(string matchedVerb, string[] cliArgs)
    {
        var verbWordCount = matchedVerb.Split(' ').Length;
        var positional = new System.Collections.Generic.List<string>();
        for (int i = 0; i < cliArgs.Length; i++)
        {
            if (cliArgs[i] == "--channel" && i + 1 < cliArgs.Length) { i++; continue; }
            if (cliArgs[i] == "--filter" && i + 1 < cliArgs.Length) { i++; continue; }
            if (cliArgs[i] == "--source" && i + 1 < cliArgs.Length) { i++; continue; }
            if (cliArgs[i] == "--json") continue;
            positional.Add(cliArgs[i]);
        }
        return positional.Skip(verbWordCount).ToArray();
    }

    private static string? FlagValue(string[] args, string flag)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == flag && i + 1 < args.Length)
                return args[i + 1];
            if (args[i].StartsWith(flag + "=", System.StringComparison.Ordinal))
                return args[i][(flag.Length + 1)..];
        }
        return null;
    }
}
