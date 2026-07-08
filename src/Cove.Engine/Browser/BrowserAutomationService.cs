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
