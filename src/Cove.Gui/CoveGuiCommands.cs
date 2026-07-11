using System.Text.Json;
using Cove.Protocol;
using Ryn.Ipc;

namespace Cove.Gui;

public sealed class CoveGuiCommands
{
    private readonly EngineLink _link;
    public CoveGuiCommands(EngineLink link) => _link = link;

    [RynCommand("app.nookList")]
    public async ValueTask<string> NookList(CancellationToken ct)
        => await Call("cove://commands/nook.list", null, ct);

    [RynCommand("app.nookSpawn")]
    public async ValueTask<string> NookSpawn(string command, string cwd, string inheritCwdFrom, int cols, int rows, string adapter, string agentName, string bay, string shore, string[]? args = null, string? sessionId = null, bool yolo = false, CancellationToken ct = default)
    {
        var shell = string.IsNullOrEmpty(command) ? DefaultShell() : command;
        var p = JsonSerializer.SerializeToElement(new SpawnParams(shell, args ?? Array.Empty<string>(), N(cwd), null, cols, rows, N(inheritCwdFrom), N(adapter), N(agentName), N(bay), N(shore), SessionId: N(sessionId ?? ""), Yolo: yolo), CoveJsonContext.Default.SpawnParams);
        return await Call("cove://commands/nook.spawn", p, ct);
    }

    [RynCommand("app.feedbackSave")]
    public ValueTask<string> FeedbackSave(string json, string slug)
    {
        var dd = Cove.Platform.CoveDataDir.Resolve(ParseChannel(_link.Channel));
        var dir = System.IO.Path.Combine(dd.Root, "feedback");
        System.IO.Directory.CreateDirectory(dir);
        var stamp = System.DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var safeSlug = string.IsNullOrWhiteSpace(slug) ? "ui-feedback" : slug;
        var path = System.IO.Path.Combine(dir, $"{stamp}-{safeSlug}.json");
        System.IO.File.WriteAllText(path, json);
        return ValueTask.FromResult($"{{\"path\":\"{System.Text.Json.JsonEncodedText.Encode(path)}\"}}");
    }

    private static Cove.Platform.CoveChannel ParseChannel(string channel) => channel switch
    {
        "beta" => Cove.Platform.CoveChannel.Beta,
        "dev" => Cove.Platform.CoveChannel.Dev,
        _ => Cove.Platform.CoveChannel.Stable,
    };

    [RynCommand("app.nookWrite")]
    public async ValueTask<string> NookWrite(string nookId, string dataBase64, CancellationToken ct)
        => await Call("cove://commands/nook.write", NookIdParam(nookId, dataBase64), ct);

    [RynCommand("app.nookResize")]
    public async ValueTask<string> NookResize(string nookId, int cols, int rows, CancellationToken ct)
    {
        var p = JsonSerializer.SerializeToElement(new ResizeParams(nookId, cols, rows), CoveJsonContext.Default.ResizeParams);
        return await Call("cove://commands/nook.resize", p, ct);
    }

    [RynCommand("app.nookKill")]
    public async ValueTask<string> NookKill(string nookId, CancellationToken ct)
        => await Call("cove://commands/nook.kill", NookIdParam(nookId), ct);

    [RynCommand("app.nookRename")]
    public async ValueTask<string> NookRename(string nookId, string title, CancellationToken ct)
    {
        var p = JsonSerializer.SerializeToElement(new NookRenameParams(nookId, title), CoveJsonContext.Default.NookRenameParams);
        return await Call("cove://commands/nook.rename", p, ct);
    }

    [RynCommand("app.nookSearch")]
    public async ValueTask<string> NookSearch(string nookId, string query, bool caseSensitive, CancellationToken ct)
    {
        var p = JsonSerializer.SerializeToElement(new SearchParams(nookId, query, caseSensitive), CoveJsonContext.Default.SearchParams);
        return await Call("cove://commands/nook.search", p, ct);
    }

    [RynCommand("app.layoutGet")]
    public async ValueTask<string> LayoutGet(CancellationToken ct)
        => await Call("cove://commands/layout.get", null, ct);

    [RynCommand("app.layoutMutate")]
    public async ValueTask<string> LayoutMutate(string op, string shoreId, string targetNookId, string newNookId, string orientation, string name, string nookId, int dir, string nookType = "", string[]? shoreIds = null, CancellationToken ct = default)
    {
        var mp = new LayoutMutateParams(op, N(shoreId), N(targetNookId), N(newNookId), N(orientation), N(name), N(nookId), dir, N(nookType), shoreIds);
        var p = JsonSerializer.SerializeToElement(mp, CoveJsonContext.Default.LayoutMutateParams);
        return await Call("cove://commands/layout.mutate", p, ct);
    }

    [RynCommand("app.sessionState")]
    public async ValueTask<string> SessionState(string nookId, CancellationToken ct)
    {
        var p = JsonSerializer.SerializeToElement(new NookRefParams(nookId), CoveJsonContext.Default.NookRefParams);
        return await Call("cove://commands/session.state", p, ct);
    }

    [RynCommand("app.configGet")]
    public async ValueTask<string> ConfigGet(string key, CancellationToken ct)
    {
        var p = JsonSerializer.SerializeToElement(new ConfigGetParams(key), CoveJsonContext.Default.ConfigGetParams);
        return await Call("cove://commands/config.get", p, ct);
    }

    [RynCommand("app.configSet")]
    public async ValueTask<string> ConfigSet(string key, string value, CancellationToken ct)
    {
        var p = JsonSerializer.SerializeToElement(new ConfigSetParams(key, value), CoveJsonContext.Default.ConfigSetParams);
        return await Call("cove://commands/config.set", p, ct);
    }

    private static JsonElement NookIdParam(string nookId, string? dataBase64 = null)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("nookId", nookId);
            if (dataBase64 is not null) w.WriteString("dataBase64", dataBase64);
            w.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(ms.ToArray());
        return doc.RootElement.Clone();
    }

    private static string? N(string s) => string.IsNullOrEmpty(s) ? null : s;

    [RynCommand("app.fsList")]
    public ValueTask<string> FsList(string path)
        => ValueTask.FromResult(FsListing.ListDirectory(path, 400));

    [RynCommand("app.gitSummary")]
    public ValueTask<string> GitSummaryFor(string path)
        => ValueTask.FromResult(GitSummary.Run(path));

    [RynCommand("app.adapterList")]
    public async ValueTask<string> AdapterList(CancellationToken ct)
        => await Call("cove://commands/adapter.list", null, ct);

    private static string DefaultShell()
        => OperatingSystem.IsWindows() ? "powershell.exe" : (Environment.GetEnvironmentVariable("SHELL") ?? "/bin/zsh");

    [RynCommand("app.callEngine")]
    public async ValueTask<string> CallEngine(string uri, string argsJson, CancellationToken ct)
    {
        JsonElement? args = null;
        if (!string.IsNullOrEmpty(argsJson) && argsJson != "null")
        {
            using var doc = JsonDocument.Parse(argsJson);
            args = doc.RootElement.Clone();
        }
        var r = await _link.RequestAsync(uri, args, ct);
        if (!r.Ok)
            throw new InvalidOperationException(r.Error?.Message ?? r.Error?.Code ?? "engine_error");
        return r.Data is { } d ? d.GetRawText() : "{}";
    }
    private async ValueTask<string> Call(string uri, JsonElement? p, CancellationToken ct)
    {
        var r = await _link.RequestAsync(uri, p, ct);
        return r.Data is { } d ? d.GetRawText() : "{}";
    }
}
