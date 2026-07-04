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
    public async ValueTask<string> PaneSpawn(string command, int cols, int rows, CancellationToken ct)
    {
        var shell = string.IsNullOrEmpty(command) ? DefaultShell() : command;
        var p = JsonSerializer.SerializeToElement(new SpawnParams(shell, Array.Empty<string>(), null, null, cols, rows), CoveJsonContext.Default.SpawnParams);
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

    private static string DefaultShell()
        => OperatingSystem.IsWindows() ? "powershell.exe" : (Environment.GetEnvironmentVariable("SHELL") ?? "/bin/zsh");

    private async ValueTask<string> Call(string uri, JsonElement? p, CancellationToken ct)
    {
        var r = await _link.RequestAsync(uri, p, ct);
        return r.Data is { } d ? d.GetRawText() : "{}";
    }
}
