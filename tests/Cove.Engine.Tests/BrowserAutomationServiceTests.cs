using System.Diagnostics;
using System.Text.Json;
using Cove.Engine.Browser;
using Cove.Testing;
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

    private Process _browser = null!;
    private bool _browserStarted;
    private int _port;
    private string _wsUrl = "";
    private string _profileDir = "";
    private BrowserAutomationService _service = null!;
    private CdpClient _client = null!;
    private int _disposed;

    public async Task InitializeAsync()
    {
        var binary = Assert.Single(BrowserSearchPaths.Where(System.IO.File.Exists).Take(1));

        try
        {
            _port = 9400 + Random.Shared.Next(0, 200);
            _profileDir = TestDirectory.Create("cdp-auto");

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
            psi.ArgumentList.Add($"--user-data-dir={_profileDir}");
            psi.ArgumentList.Add("about:blank");

            _browser = new Process { StartInfo = psi };
            _browserStarted = _browser.Start();

            using var http = new HttpClient();
            await AsyncTest.EventuallyAsync(async () =>
            {
                try
                {
                    var listJson = await http.GetStringAsync($"http://127.0.0.1:{_port}/json/list");
                    using var targets = JsonDocument.Parse(listJson);
                    var page = targets.RootElement.EnumerateArray()
                        .FirstOrDefault(target => target.GetProperty("type").GetString() == "page");
                    if (page.ValueKind == JsonValueKind.Undefined)
                        return false;
                    _wsUrl = page.GetProperty("webSocketDebuggerUrl").GetString() ?? "";
                    return _wsUrl.Length > 0;
                }
                catch (HttpRequestException)
                {
                    return false;
                }
            }, TimeSpan.FromSeconds(10), "headless browser did not start within 10s");

            _service = new BrowserAutomationService();
            _client = await _service.ConnectAsync(_wsUrl);
        }
        catch (Exception initializationException)
        {
            try
            {
                await DisposeAsync();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(initializationException, cleanupException);
            }
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var failures = new List<Exception>();
        if (_service is not null)
        {
            try
            {
                await _service.DisposeAsync();
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }
        if (_browser is not null)
        {
            if (_browserStarted)
            {
                try
                {
                    if (!_browser.HasExited)
                        _browser.Kill(entireProcessTree: true);
                    await TestProcess.WaitForExitAsync(_browser, TimeSpan.FromSeconds(10));
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            }
            try
            {
                _browser.Dispose();
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }
        if (_profileDir.Length > 0)
        {
            try
            {
                TestDirectory.Delete(_profileDir);
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }
        if (failures.Count > 0)
            throw new AggregateException("browser fixture cleanup failed", failures);
    }

    private async Task NavigateAsync(string url)
    {
        await _service.NavigateAsync(_client, url);
        await _service.WaitAsync(_client, "document.readyState === 'complete'", 5000);
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task EvalAsync_ReturnsExpressionValue()
    {
        var result = await _service.EvalAsync(_client, "6 * 7", true);
        var value = result.GetProperty("result").GetProperty("value").GetInt32();
        Assert.Equal(42, value);
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task NavigateAsync_LoadsUrlAndEvalGetsTitle()
    {
        await NavigateAsync("data:text/html,<title>Nav Test</title><h1>Hi</h1>");

        var result = await _service.EvalAsync(_client, "document.title", true);
        Assert.Equal("Nav Test", result.GetProperty("result").GetProperty("value").GetString());
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task ClickAsync_ClicksElement()
    {
        var html = "data:text/html,<button onclick=\"this.textContent='clicked'\">click me</button>";
        await NavigateAsync(html);

        await _service.ClickAsync(_client, "button");
        await _service.WaitAsync(_client, "document.querySelector('button').textContent === 'clicked'", 5000);
        var result = await _service.EvalAsync(_client, "document.querySelector('button').textContent", true);
        Assert.Equal("clicked", result.GetProperty("result").GetProperty("value").GetString());
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task FillAsync_SetsInputValue()
    {
        var html = "data:text/html,<input id='i' type='text'>";
        await NavigateAsync(html);

        await _service.FillAsync(_client, "#i", "hello world");
        var result = await _service.EvalAsync(_client, "document.querySelector('#i').value", true);
        Assert.Equal("hello world", result.GetProperty("result").GetProperty("value").GetString());
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task ClearAsync_ClearsInputValue()
    {
        var html = "data:text/html,<input id='i' type='text' value='existing'>";
        await NavigateAsync(html);

        await _service.ClearAsync(_client, "#i");
        var result = await _service.EvalAsync(_client, "document.querySelector('#i').value", true);
        Assert.Equal("", result.GetProperty("result").GetProperty("value").GetString());
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task TypeAsync_DispatchesCharacterKeys()
    {
        var html = "data:text/html,<input id='i' autofocus>";
        await NavigateAsync(html);

        await _service.EvalAsync(_client, "document.querySelector('#i').focus()", false);
        await _service.TypeAsync(_client, "abc");
        await _service.WaitAsync(_client, "document.querySelector('#i').value === 'abc'", 5000);
        var result = await _service.EvalAsync(_client, "document.querySelector('#i').value", true);
        Assert.Equal("abc", result.GetProperty("result").GetProperty("value").GetString());
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task ScreenshotAsync_ReturnsPngBase64()
    {
        await NavigateAsync("data:text/html,<h1>screenshot</h1>");

        var data = await _service.ScreenshotAsync(_client);
        Assert.NotEmpty(data);
        Assert.True(data.Length > 100);
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task GetSnapshotAsync_ReturnsAccessibilityTree()
    {
        var html = "data:text/html,<button>Test Button</button>";
        await NavigateAsync(html);

        var tree = await _service.GetSnapshotAsync(_client);
        var raw = tree.GetRawText();
        Assert.NotEmpty(raw);
        Assert.Contains("nodes", raw);
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task WaitAsync_ResolvesWhenConditionTrue()
    {
        var html = "data:text/html,<div id='d'>not ready</div>";
        await NavigateAsync(html);

        await _service.EvalAsync(_client, "setTimeout(()=>{document.querySelector('#d').textContent='ready'},200)", false);
        await _service.WaitAsync(_client, "document.querySelector('#d').textContent==='ready'", 3000);
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task WaitAsync_TimeoutWhenConditionNeverTrue()
    {
        var html = "data:text/html,<div id='d'>nope</div>";
        await NavigateAsync(html);

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await _service.WaitAsync(_client, "document.querySelector('#d').textContent==='never'", 1000));
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task SelectAsync_SetsSelectValue()
    {
        var html = "data:text/html,<select id='s'><option value='a'>A</option><option value='b'>B</option></select>";
        await NavigateAsync(html);

        await _service.SelectAsync(_client, "#s", new[] { "b" });
        var result = await _service.EvalAsync(_client, "document.querySelector('#s').value", true);
        Assert.Equal("b", result.GetProperty("result").GetProperty("value").GetString());
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task ScrollAsync_DoesNotError()
    {
        var html = "data:text/html,<div style='height:3000px'>tall</div>";
        await NavigateAsync(html);

        await _service.ScrollAsync(_client, 0, 500);
        var result = await _service.EvalAsync(_client, "window.scrollY > 0", true);
        Assert.True(result.GetProperty("result").GetProperty("value").GetBoolean());
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task PressAsync_DispatchesKeyEvent()
    {
        var html = "data:text/html,<input id='i' autofocus>";
        await NavigateAsync(html);

        await _service.EvalAsync(_client, "document.querySelector('#i').focus()", false);
        await _service.PressAsync(_client, "a");
        await _service.WaitAsync(_client, "document.querySelector('#i').value === 'a'", 5000);
        var result = await _service.EvalAsync(_client, "document.querySelector('#i').value", true);
        Assert.Equal("a", result.GetProperty("result").GetProperty("value").GetString());
    }
    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task GetTextAsync_ReturnsElementText()
    {
        var html = "data:text/html,<div id='d'>hello text</div>";
        await NavigateAsync(html);

        var result = await _service.GetTextAsync(_client, "#d");
        Assert.Equal("hello text", result.GetProperty("result").GetProperty("value").GetString());
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task IsVisibleAsync_TrueForVisibleElement()
    {
        var html = "data:text/html,<div id='v' style='width:100px;height:50px'>vis</div>";
        await NavigateAsync(html);

        Assert.True(await _service.IsVisibleAsync(_client, "#v"));
        Assert.False(await _service.IsVisibleAsync(_client, "#nonexistent"));
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task ListElementsAsync_ReturnsMatchingElements()
    {
        var html = "data:text/html,<div id='a' class='x'>A</div><div id='b' class='y'>B</div>";
        await NavigateAsync(html);

        var list = await _service.ListElementsAsync(_client, "div");
        Assert.Equal(2, list.Count);
        Assert.Contains(list, s => s.StartsWith("div#"));
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task GetCookiesAsync_ReturnsCookieArray()
    {
        await NavigateAsync("data:text/html,<h1>cookies</h1>");

        var result = await _service.GetCookiesAsync(_client);
        Assert.True(result.TryGetProperty("cookies", out var cookies));
        Assert.Equal(JsonValueKind.Array, cookies.ValueKind);
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task SetCookieAsync_CreatesCookie()
    {
        await _service.SetCookieAsync(_client, "testcookie", "testvalue", "https://example.com");
        using var query = JsonDocument.Parse("""{"urls":["https://example.com"]}""");
        var cookiesResult = await _service.PassthroughAsync(_client, "Network.getCookies", query.RootElement.Clone());
        var cookies = cookiesResult.GetProperty("cookies").EnumerateArray();
        Assert.Contains(cookies, c => c.GetProperty("name").GetString() == "testcookie" && c.GetProperty("value").GetString() == "testvalue");
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task ClearCookiesAsync_RemovesAllCookies()
    {
        await _service.SetCookieAsync(_client, "c1", "v1", "https://example.com");
        await _service.ClearCookiesAsync(_client);
        using var query = JsonDocument.Parse("""{"urls":["https://example.com"]}""");
        var cookiesResult = await _service.PassthroughAsync(_client, "Network.getCookies", query.RootElement.Clone());
        var cookies = cookiesResult.GetProperty("cookies").EnumerateArray();
        Assert.Empty(cookies);
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task GetConsoleEntriesAsync_CapturesConsoleLog()
    {
        var html = "data:text/html,<script>console.log('test-entry')</script>";
        await NavigateAsync(html);

        var entries = await _service.GetConsoleEntriesAsync(_client, 2000);
        Assert.Contains(entries, e => e.Text.Contains("test-entry"));
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task PassthroughAsync_SendsRawCdpCommand()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""{"expression":"1+1","returnByValue":true}""");
        var result = await _service.PassthroughAsync(_client, "Runtime.evaluate", doc.RootElement.Clone());
        Assert.Equal(2, result.GetProperty("result").GetProperty("value").GetInt32());
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task AssignRefsAsync_ClickRefTargetsCorrectElement()
    {
        var html = "data:text/html,<button id='b1' onclick='this.textContent=\"one-clicked\"'>one</button><button id='b2' onclick='this.textContent=\"two-clicked\"'>two</button>";
        await NavigateAsync(html);


        var refs = await _service.AssignRefsAsync(_client);
        var buttonRefs = refs.Where(r => r.Role == "button").ToList();
        Assert.True(buttonRefs.Count >= 2, $"expected >=2 button refs, got {buttonRefs.Count}");

        var secondRef = buttonRefs[1];
        await _service.ClickRefAsync(_client, secondRef.Id);
        await _service.WaitAsync(_client, "document.querySelector('#b2').textContent === 'two-clicked'", 5000);

        var b1Text = (await _service.EvalAsync(_client, "document.querySelector('#b1').textContent", true)).GetProperty("result").GetProperty("value").GetString();
        var b2Text = (await _service.EvalAsync(_client, "document.querySelector('#b2').textContent", true)).GetProperty("result").GetProperty("value").GetString();

        Assert.Equal("two-clicked", b2Text);
        Assert.Equal("one", b1Text);
    }
}
