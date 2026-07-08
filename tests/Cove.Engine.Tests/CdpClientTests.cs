using System.Diagnostics;
using System.Text.Json;
using Cove.Engine.Browser;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CdpClientTests : IAsyncLifetime
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

    public async Task InitializeAsync()
    {
        var binary = BrowserSearchPaths.FirstOrDefault(System.IO.File.Exists);
        if (binary is null)
        {
            _port = -1;
            return;
        }

        _port = 9300 + Random.Shared.Next(0, 200);
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cdp-test-{Guid.NewGuid():N}");
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
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null && !_browser.HasExited)
        {
            try { _browser.Kill(); } catch { }
            await _browser.WaitForExitAsync();
        }
        _browser?.Dispose();
    }

    [Fact]
    public async Task ConnectAndSend_EvaluatesExpression()
    {
        if (_port < 0) return;

        await using var client = new CdpClient(_wsUrl);
        await Task.Delay(500);

        using var paramsDoc = JsonDocument.Parse("""{"expression":"1 + 2 + 3","returnByValue":true}""");
        var result = await client.SendAsync("Runtime.evaluate", paramsDoc.RootElement.Clone());

        var value = result.GetProperty("result").GetProperty("value").GetInt32();
        Assert.Equal(6, value);
    }

    [Fact]
    public async Task Navigate_LoadsPageAndGetsTitle()
    {
        if (_port < 0) return;

        await using var client = new CdpClient(_wsUrl);
        await Task.Delay(500);

        using var navParams = JsonDocument.Parse("""{"url":"data:text/html,<title>Test Page</title><h1>Hello</h1>"}""");
        await client.SendAsync("Page.navigate", navParams.RootElement.Clone());
        await Task.Delay(500);

        using var evalParams = JsonDocument.Parse("""{"expression":"document.title","returnByValue":true}""");
        var result = await client.SendAsync("Runtime.evaluate", evalParams.RootElement.Clone());
        var title = result.GetProperty("result").GetProperty("value").GetString();
        Assert.Equal("Test Page", title);
    }

    [Fact]
    public async Task Subscribe_ReceivesConsoleEvent()
    {
        if (_port < 0) return;

        await using var client = new CdpClient(_wsUrl);
        await Task.Delay(500);

        using var enableParams = JsonDocument.Parse("{}");
        await client.SendAsync("Runtime.enable", enableParams.RootElement.Clone());

        using var navParams = JsonDocument.Parse("""{"url":"data:text/html,<script>console.log('hello-cdp')</script>"}""");
        await client.SendAsync("Page.navigate", navParams.RootElement.Clone());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        string? receivedText = null;
        await foreach (var evt in client.SubscribeAsync("Runtime.consoleAPICalled", cts.Token))
        {
            receivedText = evt.Params.GetProperty("args")[0].GetProperty("value").GetString();
            break;
        }

        Assert.Equal("hello-cdp", receivedText);
    }
}
