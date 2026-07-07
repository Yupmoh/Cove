using System.Text.Json;
using Cove.Protocol;
using Ryn.Ipc;

namespace Cove.Gui;

public sealed class CoveGuiCommands
{
    private readonly EngineLink _link;
    public CoveGuiCommands(EngineLink link) => _link = link;

    [RynCommand("app.paneList")]
    public async ValueTask<string> PaneList(CancellationToken ct)
        => await Call("cove://commands/pane.list", null, ct);

    [RynCommand("app.paneSpawn")]
    public async ValueTask<string> PaneSpawn(string command, string cwd, string inheritCwdFrom, int cols, int rows, string adapter, string agentName, string workspace, string room, CancellationToken ct)
    {
        var shell = string.IsNullOrEmpty(command) ? DefaultShell() : command;
        var p = JsonSerializer.SerializeToElement(new SpawnParams(shell, Array.Empty<string>(), N(cwd), null, cols, rows, N(inheritCwdFrom), N(adapter), N(agentName), N(workspace), N(room)), CoveJsonContext.Default.SpawnParams);
        return await Call("cove://commands/pane.spawn", p, ct);
    }

    [RynCommand("app.paneWrite")]
    public async ValueTask<string> PaneWrite(string paneId, string dataBase64, CancellationToken ct)
        => await Call("cove://commands/pane.write", PaneIdParam(paneId, dataBase64), ct);

    [RynCommand("app.paneResize")]
    public async ValueTask<string> PaneResize(string paneId, int cols, int rows, CancellationToken ct)
    {
        var p = JsonSerializer.SerializeToElement(new ResizeParams(paneId, cols, rows), CoveJsonContext.Default.ResizeParams);
        return await Call("cove://commands/pane.resize", p, ct);
    }

    [RynCommand("app.paneKill")]
    public async ValueTask<string> PaneKill(string paneId, CancellationToken ct)
        => await Call("cove://commands/pane.kill", PaneIdParam(paneId), ct);

    [RynCommand("app.paneRename")]
    public async ValueTask<string> PaneRename(string paneId, string title, CancellationToken ct)
    {
        var p = JsonSerializer.SerializeToElement(new PaneRenameParams(paneId, title), CoveJsonContext.Default.PaneRenameParams);
        return await Call("cove://commands/pane.rename", p, ct);
    }

    [RynCommand("app.layoutGet")]
    public async ValueTask<string> LayoutGet(CancellationToken ct)
        => await Call("cove://commands/layout.get", null, ct);

    [RynCommand("app.layoutMutate")]
    public async ValueTask<string> LayoutMutate(string op, string roomId, string targetPaneId, string newPaneId, string orientation, string name, string paneId, int dir, CancellationToken ct)
    {
        var mp = new LayoutMutateParams(op, N(roomId), N(targetPaneId), N(newPaneId), N(orientation), N(name), N(paneId), dir);
        var p = JsonSerializer.SerializeToElement(mp, CoveJsonContext.Default.LayoutMutateParams);
        return await Call("cove://commands/layout.mutate", p, ct);
    }

    [RynCommand("app.sessionState")]
    public async ValueTask<string> SessionState(string paneId, CancellationToken ct)
    {
        var p = JsonSerializer.SerializeToElement(new PaneRefParams(paneId), CoveJsonContext.Default.PaneRefParams);
        return await Call("cove://commands/session.state", p, ct);
    }


    private static JsonElement PaneIdParam(string paneId, string? dataBase64 = null)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("paneId", paneId);
            if (dataBase64 is not null) w.WriteString("dataBase64", dataBase64);
            w.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(ms.ToArray());
        return doc.RootElement.Clone();
    }

    private static string? N(string s) => string.IsNullOrEmpty(s) ? null : s;

    [RynCommand("app.adapterList")]
    public async ValueTask<string> AdapterList(CancellationToken ct)
        => await Call("cove://commands/adapter.list", null, ct);

    private static string DefaultShell()
        => OperatingSystem.IsWindows() ? "powershell.exe" : (Environment.GetEnvironmentVariable("SHELL") ?? "/bin/zsh");

    private async ValueTask<string> Call(string uri, JsonElement? p, CancellationToken ct)
    {
        var r = await _link.RequestAsync(uri, p, ct);
        return r.Data is { } d ? d.GetRawText() : "{}";
    }
}
