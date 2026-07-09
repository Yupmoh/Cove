using System.Text.Json;
using System.Text.Json.Serialization;
using Ryn.Core;

namespace Cove.Gui;

public sealed class EngineEventForwarder
{
    private readonly EngineLink _link;
    private readonly IRynWindowManager _windows;

    public EngineEventForwarder(EngineLink link, IRynWindowManager windows)
    {
        _link = link;
        _windows = windows;
        _link.SetEngineEventHandler(Forward);
    }
    private void Forward(string channel, JsonElement payload)
    {
        var window = _windows.Windows.Count > 0 ? _windows.Windows[0] : null;
        if (window is not RynWindow rynWindow) return;
        var webView = rynWindow.WebView;
        if (webView is null) return;
        var evt = new EngineEventPayload(channel, payload);
        webView.EmitEvent("engine.event", JsonSerializer.Serialize(evt, EngineEventPayloadJsonContext.Default.EngineEventPayload));
    }
}

public sealed record EngineEventPayload(string Channel, JsonElement Payload);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EngineEventPayload))]
public sealed partial class EngineEventPayloadJsonContext : JsonSerializerContext { }
