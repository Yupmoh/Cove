using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Browser;

public sealed class BrowserAutomationService : IAsyncDisposable
{
    private readonly Dictionary<string, CdpClient> _sessions = new();
    private readonly ILogger _logger;

    public BrowserAutomationService(ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public async Task<CdpClient> ConnectAsync(string wsUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(wsUrl))
            throw new ArgumentException("wsUrl required", nameof(wsUrl));

        var client = new CdpClient(wsUrl, _logger);
        _sessions[wsUrl] = client;
        return client;
    }

    public CdpClient? GetSession(string wsUrl)
    {
        return _sessions.TryGetValue(wsUrl, out var c) ? c : null;
    }

    public async Task<JsonElement> EvalAsync(CdpClient client, string expression, bool returnByValue = true, CancellationToken ct = default)
    {
        var json = $$"""{"expression":{{JsonString(expression)}},"returnByValue":{{returnByValue.ToString().ToLowerInvariant()}}}""";
        using var doc = JsonDocument.Parse(json);
        return await client.SendAsync("Runtime.evaluate", doc.RootElement.Clone(), ct).ConfigureAwait(false);
    }

    public async Task NavigateAsync(CdpClient client, string url, CancellationToken ct = default)
    {
        var json = $$"""{"url":{{JsonString(url)}}}""";
        using var doc = JsonDocument.Parse(json);
        await client.SendAsync("Page.navigate", doc.RootElement.Clone(), ct).ConfigureAwait(false);
    }

    public async Task ClickAsync(CdpClient client, string selector, CancellationToken ct = default)
    {
        var expr = $$"""document.querySelector({{JsonString(selector)}})?.click()""";
        await EvalAsync(client, expr, false, ct).ConfigureAwait(false);
    }

    public async Task FillAsync(CdpClient client, string selector, string value, CancellationToken ct = default)
    {
        var expr = $$"""(function(){var el=document.querySelector({{JsonString(selector)}});if(el){el.value={{JsonString(value)}};el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));return true;}return false;})()""";
        await EvalAsync(client, expr, true, ct).ConfigureAwait(false);
    }

    public async Task TypeAsync(CdpClient client, string text, CancellationToken ct = default)
    {
        foreach (var ch in text)
        {
            var json = $$"""{"type":"char","text":{{JsonString(ch.ToString())}}}""";
            using var doc = JsonDocument.Parse(json);
            await client.SendAsync("Input.dispatchKeyEvent", doc.RootElement.Clone(), ct).ConfigureAwait(false);
        }
    }

    public async Task PressAsync(CdpClient client, string key, CancellationToken ct = default)
    {
        if (key.Length == 1)
        {
            var jsonChar = $$"""{"type":"char","text":{{JsonString(key)}}}""";
            using var docChar = JsonDocument.Parse(jsonChar);
            await client.SendAsync("Input.dispatchKeyEvent", docChar.RootElement.Clone(), ct).ConfigureAwait(false);
        }
        else
        {
            var json = $$"""{"type":"rawKeyDown","key":{{JsonString(key)}}}""";
            using var doc = JsonDocument.Parse(json);
            await client.SendAsync("Input.dispatchKeyEvent", doc.RootElement.Clone(), ct).ConfigureAwait(false);
            var jsonUp = $$"""{"type":"keyUp","key":{{JsonString(key)}}}""";
            using var docUp = JsonDocument.Parse(jsonUp);
            await client.SendAsync("Input.dispatchKeyEvent", docUp.RootElement.Clone(), ct).ConfigureAwait(false);
        }
    }

    public async Task ScrollAsync(CdpClient client, int deltaX, int deltaY, CancellationToken ct = default)
    {
        var expr = $$"""window.scrollBy({{deltaX}},{{deltaY}})""";
        await EvalAsync(client, expr, false, ct).ConfigureAwait(false);
    }
    public async Task<string> ScreenshotAsync(CdpClient client, CancellationToken ct = default)
    {
        var json = """{"format":"png"}""";
        using var doc = JsonDocument.Parse(json);
        var result = await client.SendAsync("Page.captureScreenshot", doc.RootElement.Clone(), ct).ConfigureAwait(false);
        return result.GetProperty("data").GetString() ?? "";
    }

    public async Task<JsonElement> GetSnapshotAsync(CdpClient client, CancellationToken ct = default)
    {
        await client.SendAsync("Accessibility.enable", default(JsonElement?), ct).ConfigureAwait(false);
        var json = """{"world":"MAIN"}""";
        using var doc = JsonDocument.Parse(json);
        return await client.SendAsync("Accessibility.getFullAXTree", doc.RootElement.Clone(), ct).ConfigureAwait(false);
    }

    public async Task<JsonElement> SelectAsync(CdpClient client, string selector, string[] values, CancellationToken ct = default)
    {
        var valuesJson = string.Join(",", values.Select(JsonString));
        if (valuesJson.Length == 0) valuesJson = "\"\"";
        var expr = $$"""(function(){var el=document.querySelector({{JsonString(selector)}});if(el){el.value={{valuesJson}};el.dispatchEvent(new Event('change',{bubbles:true}));return true;}return false;})()""";
        return await EvalAsync(client, expr, true, ct).ConfigureAwait(false);
    }

    public async Task ClearAsync(CdpClient client, string selector, CancellationToken ct = default)
    {
        var expr = $$"""(function(){var el=document.querySelector({{JsonString(selector)}});if(el){el.value='';el.dispatchEvent(new Event('input',{bubbles:true}));return true;}return false;})()""";
        await EvalAsync(client, expr, true, ct).ConfigureAwait(false);
    }

    public async Task<JsonElement> WaitAsync(CdpClient client, string expression, int timeoutMs = 5000, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var result = await EvalAsync(client, expression, true, ct).ConfigureAwait(false);
            if (result.TryGetProperty("result", out var r) && r.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.True)
                return result;
            await Task.Delay(100, ct).ConfigureAwait(false);
        }
        throw new TimeoutException($"wait condition not met within {timeoutMs}ms");
    }

    public sealed record BrowserRef(string Id, string Kind, string Role, string Name, string Selector);

    private readonly Dictionary<CdpClient, RefSession> _refSessions = new();

    private RefSession GetOrCreateRefSession(CdpClient client)
    {
        if (!_refSessions.TryGetValue(client, out var rs))
        {
            rs = new RefSession();
            _refSessions[client] = rs;
        }
        return rs;
    }

    private sealed class RefSession
    {
        public readonly Dictionary<string, BrowserRef> Refs = new();
        public int NextElementRef = 1;
        public int NextFieldRef = 1;
    }

    public async Task<IReadOnlyList<BrowserRef>> AssignRefsAsync(CdpClient client, CancellationToken ct = default)
    {
        var session = GetOrCreateRefSession(client);
        session.Refs.Clear();
        session.NextElementRef = 1;
        session.NextFieldRef = 1;

        await EvalAsync(client, "[...document.querySelectorAll('[data-cove-ref]')].forEach(el => el.removeAttribute('data-cove-ref'))", false, ct).ConfigureAwait(false);

        var tree = await GetSnapshotAsync(client, ct).ConfigureAwait(false);
        var result = new List<BrowserRef>();
        if (!tree.TryGetProperty("nodes", out var nodes)) return result;

        foreach (var node in nodes.EnumerateArray())
        {
            var role = node.TryGetProperty("role", out var r) ? r.GetProperty("value").GetString() ?? "" : "";
            var name = node.TryGetProperty("name", out var n) ? n.GetProperty("value").GetString() ?? "" : "";
            var backendNodeId = node.TryGetProperty("backendDOMNodeId", out var bn) ? bn.GetInt32() : 0;
            if (backendNodeId == 0) continue;

            var isField = role is "textbox" or "searchbox" or "combobox" or "spinbutton" or "slider";
            var prefix = isField ? "f" : "e";
            var num = isField ? session.NextFieldRef++ : session.NextElementRef++;
            var refId = $"{prefix}{num}";

            var selector = await MarkNodeWithRefAsync(client, backendNodeId, refId, ct).ConfigureAwait(false);
            var browserRef = new BrowserRef(refId, isField ? "field" : "element", role, name, selector);
            session.Refs[refId] = browserRef;
            result.Add(browserRef);
        }
        return result;
    }

    public BrowserRef? GetRef(CdpClient client, string refId)
    {
        return _refSessions.TryGetValue(client, out var rs) && rs.Refs.TryGetValue(refId, out var r) ? r : null;
    }

    public async Task ClickRefAsync(CdpClient client, string refId, CancellationToken ct = default)
    {
        var browserRef = ResolveRef(client, refId);
        await ClickAsync(client, browserRef.Selector, ct).ConfigureAwait(false);
    }

    public async Task FillRefAsync(CdpClient client, string refId, string value, CancellationToken ct = default)
    {
        var browserRef = ResolveRef(client, refId);
        await FillAsync(client, browserRef.Selector, value, ct).ConfigureAwait(false);
    }

    private BrowserRef ResolveRef(CdpClient client, string refId)
    {
        if (!_refSessions.TryGetValue(client, out var rs) || !rs.Refs.TryGetValue(refId, out var r))
            throw new ArgumentException($"unknown ref: {refId}", nameof(refId));
        return r;
    }

    private async Task<string> MarkNodeWithRefAsync(CdpClient client, int backendNodeId, string refId, CancellationToken ct)
    {
        var resolveJson = $$"""{"backendNodeId":{{backendNodeId}},"objectGroup":"cove","returnByValue":false}""";
        using var resolveDoc = JsonDocument.Parse(resolveJson);
        var resolveResult = await client.SendAsync("DOM.resolveNode", resolveDoc.RootElement.Clone(), ct).ConfigureAwait(false);
        if (!resolveResult.TryGetProperty("object", out var obj) || !obj.TryGetProperty("objectId", out var oid))
            return $$"""[data-cove-ref="{{refId}}"]""";

        var objectId = oid.GetString()!;
        var callJson = $$"""{"functionDeclaration":"function(refId){this.setAttribute('data-cove-ref',refId);return '[data-cove-ref=\"'+refId+'\"]';}","objectId":{{JsonString(objectId)}},"arguments":[{"value":{{JsonString(refId)}}}],"returnByValue":true}""";
        using var callDoc = JsonDocument.Parse(callJson);
        var callResult = await client.SendAsync("Runtime.callFunctionOn", callDoc.RootElement.Clone(), ct).ConfigureAwait(false);

        var releaseJson = $$"""{"objectId":{{JsonString(objectId)}}}""";
        using var releaseDoc = JsonDocument.Parse(releaseJson);
        try { await client.SendAsync("Runtime.releaseObject", releaseDoc.RootElement.Clone(), ct).ConfigureAwait(false); } catch { }

        if (callResult.TryGetProperty("result", out var cr) && cr.TryGetProperty("value", out var cv) && cv.ValueKind == JsonValueKind.String)
            return cv.GetString()!;
        return $$"""[data-cove-ref="{{refId}}"]""";
    }

    public async Task<JsonElement> GetTextAsync(CdpClient client, string selector, CancellationToken ct = default)
    {
        var expr = $$"""(function(){var el=document.querySelector({{JsonString(selector)}});return el?el.textContent:'';})()""";
        return await EvalAsync(client, expr, true, ct).ConfigureAwait(false);
    }

    public async Task<bool> IsVisibleAsync(CdpClient client, string selector, CancellationToken ct = default)
    {
        var expr = $$"""(function(){var el=document.querySelector({{JsonString(selector)}});if(!el)return false;var r=el.getBoundingClientRect();return r.width>0&&r.height>0&&getComputedStyle(el).visibility!=='hidden';})()""";
        var result = await EvalAsync(client, expr, true, ct).ConfigureAwait(false);
        return result.TryGetProperty("result", out var r) && r.TryGetProperty("value", out var v) && v.GetBoolean();
    }

    public async Task<IReadOnlyList<string>> ListElementsAsync(CdpClient client, string selector, CancellationToken ct = default)
    {
        var expr = $$"""Array.from(document.querySelectorAll({{JsonString(selector)}})).map(function(el){return el.tagName.toLowerCase()+'#'+(el.id||'')+'.'+(el.className||'');})""";
        var result = await EvalAsync(client, expr, true, ct).ConfigureAwait(false);
        var list = new List<string>();
        if (result.TryGetProperty("result", out var r) && r.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in v.EnumerateArray())
                list.Add(item.GetString() ?? "");
        }
        return list;
    }

    public async Task<JsonElement> GetCookiesAsync(CdpClient client, CancellationToken ct = default)
    {
        return await client.SendAsync("Network.getCookies", default(JsonElement?), ct).ConfigureAwait(false);
    }

    public async Task SetCookieAsync(CdpClient client, string name, string value, string url, CancellationToken ct = default)
    {
        var json = $$"""{"name":{{JsonString(name)}},"value":{{JsonString(value)}},"url":{{JsonString(url)}}}""";
        using var doc = JsonDocument.Parse(json);
        await client.SendAsync("Network.setCookie", doc.RootElement.Clone(), ct).ConfigureAwait(false);
    }

    public async Task ClearCookiesAsync(CdpClient client, CancellationToken ct = default)
    {
        await client.SendAsync("Network.clearBrowserCookies", default(JsonElement?), ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ConsoleEntry>> GetConsoleEntriesAsync(CdpClient client, int timeoutMs = 2000, CancellationToken ct = default)
    {
        await client.SendAsync("Runtime.enable", default(JsonElement?), ct).ConfigureAwait(false);
        var entries = new List<ConsoleEntry>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            await foreach (var evt in client.SubscribeAsync("Runtime.consoleAPICalled", cts.Token).ConfigureAwait(false))
            {
                var type = evt.Params.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
                var args = evt.Params.TryGetProperty("args", out var a) ? a.EnumerateArray().Select(x => x.TryGetProperty("value", out var v) ? v.GetString() ?? x.GetRawText() : x.GetRawText()).ToList() : new List<string>();
                entries.Add(new ConsoleEntry(type, string.Join(" ", args)));
            }
        }
        catch (OperationCanceledException) { }
        return entries;
    }

    public sealed record ConsoleEntry(string Type, string Text);

    public async Task<JsonElement> PassthroughAsync(CdpClient client, string method, JsonElement? parameters, CancellationToken ct = default)
    {
        return await client.SendAsync(method, parameters, ct).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<CdpEvent> SubscribeAsync(CdpClient client, string method, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in client.SubscribeAsync(method, ct).ConfigureAwait(false))
            yield return evt;
    }

    private static string JsonString(string s) => System.Text.Json.JsonSerializer.Serialize(s, CdpJsonContext.Default.String);

    public async ValueTask DisposeAsync()
    {
        foreach (var kv in _sessions)
        {
            try { await kv.Value.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogWarning(ex, "cdp: error disposing session {url}", kv.Key); }
        }
        _sessions.Clear();
    }
}
