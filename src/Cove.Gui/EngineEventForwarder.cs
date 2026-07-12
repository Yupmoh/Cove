using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Ryn.Core;

namespace Cove.Gui;

public sealed class EngineEventForwarder
{
    private readonly EngineLink _link;
    private readonly IRynWindowManager _windows;
    private readonly ILogger<EngineEventForwarder> _log;

    public EngineEventForwarder(EngineLink link, IRynWindowManager windows, ILogger<EngineEventForwarder> log)
    {
        _link = link;
        _windows = windows;
        _log = log;
        _link.SetEngineEventHandler(Forward);
    }
    private void Forward(string channel, JsonElement payload)
    {
        var window = _windows.Windows.Count > 0 ? _windows.Windows[0] : null;
        if (window is not RynWindow rynWindow)
        {
            _log.EventForwardNoWindow(channel);
            return;
        }
        var webView = rynWindow.WebView;
        if (webView is null)
        {
            _log.EventForwardNoWebView(channel);
            return;
        }
        var evt = new EngineEventPayload(channel, payload);
        webView.EmitEvent("engine.event", JsonSerializer.Serialize(evt, EngineEventPayloadJsonContext.Default.EngineEventPayload));
        _log.EventForwarded(channel);
    }
}

public sealed record EngineEventPayload(string Channel, JsonElement Payload);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(EngineEventPayload))]
public sealed partial class EngineEventPayloadJsonContext : JsonSerializerContext { }
