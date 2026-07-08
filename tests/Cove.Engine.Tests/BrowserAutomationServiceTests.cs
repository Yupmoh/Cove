using System.Diagnostics;
using System.Text.Json;
using Cove.Engine.Browser;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BrowserAutomationServiceTests : IAsyncLifetime
{
    private static readonly string[] BrowserSearchPaths =
    {
        "/Applications/Brave Browser.app/Contents/MacOS/Brave Browser",
        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
        "/Applications/Chromium.app/Contents/MacOS/Chromium",
        "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
    };

    private Process? _browser;
    private int _port;
    private string _wsUrl = "";
    private BrowserAutomationService? _service;
    private CdpClient? _client;

    public async Task InitializeAsync()
    {
        var binary = BrowserSearchPaths.FirstOrDefault(System.IO.File.Exists);
        if (binary is null)
        {
            _port = -1;
            return;
        }

        _port = 9400 + Random.Shared.Next(0, 200);
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cdp-auto-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(tempDir);

        var psi = new ProcessStartInfo(binary)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("--headless=new");
        psi.ArgumentList.Add($"--remote-debugging-port={_port}");
        psi.ArgumentList.Add("--no-first-run");
        psi.ArgumentList.Add("--no-default-browser-check");
        psi.ArgumentList.Add("--disable-gpu");
        psi.ArgumentList.Add($"--user-data-dir={tempDir}");
        psi.ArgumentList.Add("about:blank");

        _browser = new Process { StartInfo = psi };
        _browser.Start();

        using var http = new HttpClient();
        string? listJson = null;
        for (var i = 0; i < 40; i++)
        {
            await Task.Delay(250);
            try
            {
                listJson = await http.GetStringAsync($"http://127.0.0.1:{_port}/json/list");
                if (listJson.Length > 0) break;
            }
            catch { }
        }

        if (string.IsNullOrEmpty(listJson))
            throw new InvalidOperationException("headless browser did not start within 10s");

        using var listDoc = JsonDocument.Parse(listJson);
        foreach (var target in listDoc.RootElement.EnumerateArray())
        {
            if (target.GetProperty("type").GetString() == "page")
            {
                _wsUrl = target.GetProperty("webSocketDebuggerUrl").GetString()!;
                break;
            }
        }

        if (string.IsNullOrEmpty(_wsUrl))
            throw new InvalidOperationException("no page target found");

        _service = new BrowserAutomationService();
        _client = await _service.ConnectAsync(_wsUrl);
    }

    public async Task DisposeAsync()
    {
        if (_service is not null)
            await _service.DisposeAsync();
        if (_browser is not null && !_browser.HasExited)
        {
            try { _browser.Kill(); } catch { }
            await _browser.WaitForExitAsync();
        }
        _browser?.Dispose();
    }

    [Fact]
    public async Task EvalAsync_ReturnsExpressionValue()
    {
        if (_service is null || _client is null) return;

        var result = await _service.EvalAsync(_client, "6 * 7", true);
        var value = result.GetProperty("result").GetProperty("value").GetInt32();
        Assert.Equal(42, value);
    }

    [Fact]
    public async Task NavigateAsync_LoadsUrlAndEvalGetsTitle()
    {
        if (_service is null || _client is null) return;

        await _service.NavigateAsync(_client, "data:text/html,<title>Nav Test</title><h1>Hi</h1>");
        await Task.Delay(500);
        var result = await _service.EvalAsync(_client, "document.title", true);
        Assert.Equal("Nav Test", result.GetProperty("result").GetProperty("value").GetString());
    }

    [Fact]
    public async Task ClickAsync_ClicksElement()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<button onclick=\"this.textContent='clicked'\">click me</button>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        await _service.ClickAsync(_client, "button");
        await Task.Delay(200);
        var result = await _service.EvalAsync(_client, "document.querySelector('button').textContent", true);
        Assert.Equal("clicked", result.GetProperty("result").GetProperty("value").GetString());
    }

    [Fact]
    public async Task FillAsync_SetsInputValue()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<input id='i' type='text'>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        await _service.FillAsync(_client, "#i", "hello world");
        var result = await _service.EvalAsync(_client, "document.querySelector('#i').value", true);
        Assert.Equal("hello world", result.GetProperty("result").GetProperty("value").GetString());
    }

    [Fact]
    public async Task ClearAsync_ClearsInputValue()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<input id='i' type='text' value='existing'>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        await _service.ClearAsync(_client, "#i");
        var result = await _service.EvalAsync(_client, "document.querySelector('#i').value", true);
        Assert.Equal("", result.GetProperty("result").GetProperty("value").GetString());
    }

    [Fact]
    public async Task TypeAsync_DispatchesCharacterKeys()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<input id='i' autofocus>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        await _service.EvalAsync(_client, "document.querySelector('#i').focus()", false);
        await _service.TypeAsync(_client, "abc");
        await Task.Delay(200);
        var result = await _service.EvalAsync(_client, "document.querySelector('#i').value", true);
        Assert.Equal("abc", result.GetProperty("result").GetProperty("value").GetString());
    }

    [Fact]
    public async Task ScreenshotAsync_ReturnsPngBase64()
    {
        if (_service is null || _client is null) return;

        await _service.NavigateAsync(_client, "data:text/html,<h1>screenshot</h1>");
        await Task.Delay(500);
        var data = await _service.ScreenshotAsync(_client);
        Assert.NotEmpty(data);
        Assert.True(data.Length > 100);
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsAccessibilityTree()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<button>Test Button</button>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        var tree = await _service.GetSnapshotAsync(_client);
        var raw = tree.GetRawText();
        Assert.NotEmpty(raw);
        Assert.Contains("nodes", raw);
    }

    [Fact]
    public async Task WaitAsync_ResolvesWhenConditionTrue()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<div id='d'>not ready</div>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        await _service.EvalAsync(_client, "setTimeout(()=>{document.querySelector('#d').textContent='ready'},200)", false);
        await _service.WaitAsync(_client, "document.querySelector('#d').textContent==='ready'", 3000);
    }

    [Fact]
    public async Task WaitAsync_TimeoutWhenConditionNeverTrue()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<div id='d'>nope</div>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await _service.WaitAsync(_client, "document.querySelector('#d').textContent==='never'", 1000));
    }

    [Fact]
    public async Task SelectAsync_SetsSelectValue()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<select id='s'><option value='a'>A</option><option value='b'>B</option></select>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        await _service.SelectAsync(_client, "#s", new[] { "b" });
        var result = await _service.EvalAsync(_client, "document.querySelector('#s').value", true);
        Assert.Equal("b", result.GetProperty("result").GetProperty("value").GetString());
    }

    [Fact]
    public async Task ScrollAsync_DoesNotError()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<div style='height:3000px'>tall</div>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        await _service.ScrollAsync(_client, 0, 500);
        var result = await _service.EvalAsync(_client, "window.scrollY > 0", true);
        Assert.True(result.GetProperty("result").GetProperty("value").GetBoolean());
    }

    [Fact]
    public async Task PressAsync_DispatchesKeyEvent()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<input id='i' autofocus>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        await _service.EvalAsync(_client, "document.querySelector('#i').focus()", false);
        await _service.PressAsync(_client, "a");
        await Task.Delay(200);
        var result = await _service.EvalAsync(_client, "document.querySelector('#i').value", true);
        Assert.Equal("a", result.GetProperty("result").GetProperty("value").GetString());
    }
    [Fact]
    public async Task GetTextAsync_ReturnsElementText()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<div id='d'>hello text</div>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        var result = await _service.GetTextAsync(_client, "#d");
        Assert.Equal("hello text", result.GetProperty("result").GetProperty("value").GetString());
    }

    [Fact]
    public async Task IsVisibleAsync_TrueForVisibleElement()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<div id='v' style='width:100px;height:50px'>vis</div>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        Assert.True(await _service.IsVisibleAsync(_client, "#v"));
        Assert.False(await _service.IsVisibleAsync(_client, "#nonexistent"));
    }

    [Fact]
    public async Task ListElementsAsync_ReturnsMatchingElements()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<div id='a' class='x'>A</div><div id='b' class='y'>B</div>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        var list = await _service.ListElementsAsync(_client, "div");
        Assert.Equal(2, list.Count);
        Assert.Contains(list, s => s.StartsWith("div#"));
    }

    [Fact]
    public async Task GetCookiesAsync_ReturnsCookieArray()
    {
        if (_service is null || _client is null) return;

        await _service.NavigateAsync(_client, "data:text/html,<h1>cookies</h1>");
        await Task.Delay(500);
        var result = await _service.GetCookiesAsync(_client);
        Assert.True(result.TryGetProperty("cookies", out var cookies));
        Assert.Equal(JsonValueKind.Array, cookies.ValueKind);
    }

    [Fact]
    public async Task SetCookieAsync_CreatesCookie()
    {
        if (_service is null || _client is null) return;

        await _service.NavigateAsync(_client, "https://example.com");
        await Task.Delay(500);
        await _service.SetCookieAsync(_client, "testcookie", "testvalue", "https://example.com");
        var cookiesResult = await _service.GetCookiesAsync(_client);
        var cookies = cookiesResult.GetProperty("cookies").EnumerateArray();
        Assert.Contains(cookies, c => c.GetProperty("name").GetString() == "testcookie" && c.GetProperty("value").GetString() == "testvalue");
    }

    [Fact]
    public async Task ClearCookiesAsync_RemovesAllCookies()
    {
        if (_service is null || _client is null) return;

        await _service.NavigateAsync(_client, "https://example.com");
        await Task.Delay(500);
        await _service.SetCookieAsync(_client, "c1", "v1", "https://example.com");
        await _service.ClearCookiesAsync(_client);
        var cookiesResult = await _service.GetCookiesAsync(_client);
        var cookies = cookiesResult.GetProperty("cookies").EnumerateArray();
        Assert.Empty(cookies);
    }

    [Fact]
    public async Task GetConsoleEntriesAsync_CapturesConsoleLog()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<script>console.log('test-entry')</script>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);
        var entries = await _service.GetConsoleEntriesAsync(_client, 2000);
        Assert.Contains(entries, e => e.Text.Contains("test-entry"));
    }

    [Fact]
    public async Task PassthroughAsync_SendsRawCdpCommand()
    {
        if (_service is null || _client is null) return;

        using var doc = System.Text.Json.JsonDocument.Parse("""{"expression":"1+1","returnByValue":true}""");
        var result = await _service.PassthroughAsync(_client, "Runtime.evaluate", doc.RootElement.Clone());
        Assert.Equal(2, result.GetProperty("result").GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task AssignRefsAsync_ClickRefTargetsCorrectElement()
    {
        if (_service is null || _client is null) return;

        var html = "data:text/html,<button id='b1' onclick='this.textContent=\"one-clicked\"'>one</button><button id='b2' onclick='this.textContent=\"two-clicked\"'>two</button>";
        await _service.NavigateAsync(_client, html);
        await Task.Delay(500);

        var refs = await _service.AssignRefsAsync(_client);
        var buttonRefs = refs.Where(r => r.Role == "button").ToList();
        Assert.True(buttonRefs.Count >= 2, $"expected >=2 button refs, got {buttonRefs.Count}");

        var secondRef = buttonRefs[1];
        await _service.ClickRefAsync(_client, secondRef.Id);
        await Task.Delay(300);

        var b1Text = (await _service.EvalAsync(_client, "document.querySelector('#b1').textContent", true)).GetProperty("result").GetProperty("value").GetString();
        var b2Text = (await _service.EvalAsync(_client, "document.querySelector('#b2').textContent", true)).GetProperty("result").GetProperty("value").GetString();

        Assert.Equal("two-clicked", b2Text);
        Assert.Equal("one", b1Text);
    }
}
