using System.Text.Json;
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
        if (window is null) return;
        var payloadJson = payload.GetRawText();
        var js = $"window.__ryn._emit('engine.event', {{ channel: {JsonSerializer.Serialize(channel)}, payload: {payloadJson} }});";
        _ = window.EvaluateJavaScriptAsync(js);
    }
}
