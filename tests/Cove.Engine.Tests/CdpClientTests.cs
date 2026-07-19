using System.Diagnostics;
using System.Text.Json;
using Cove.Engine.Browser;
using Cove.Testing;
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

    private Process _browser = null!;
    private int _port;
    private string _wsUrl = "";
    private string _profileDir = "";

    public async Task InitializeAsync()
    {
        var binary = Assert.Single(BrowserSearchPaths.Where(System.IO.File.Exists).Take(1));

        _port = 9300 + Random.Shared.Next(0, 200);
        _profileDir = TestDirectory.Create("cdp-test");

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
        _browser.Start();

        using var http = new HttpClient();
        string? listJson = null;
        await AsyncTest.EventuallyAsync(async () =>
        {
            try
            {
                listJson = await http.GetStringAsync($"http://127.0.0.1:{_port}/json/list");
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

    }

    public async Task DisposeAsync()
    {
        if (!_browser.HasExited)
        {
            _browser.Kill(entireProcessTree: true);
            await TestProcess.WaitForExitAsync(_browser, TimeSpan.FromSeconds(10));
        }
        _browser.Dispose();
        TestDirectory.Delete(_profileDir);
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task ConnectAndSend_EvaluatesExpression()
    {
        await using var client = new CdpClient(_wsUrl);

        using var paramsDoc = JsonDocument.Parse("""{"expression":"1 + 2 + 3","returnByValue":true}""");
        var result = await client.SendAsync("Runtime.evaluate", paramsDoc.RootElement.Clone());

        var value = result.GetProperty("result").GetProperty("value").GetInt32();
        Assert.Equal(6, value);
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task Navigate_LoadsPageAndGetsTitle()
    {
        await using var client = new CdpClient(_wsUrl);

        using var navParams = JsonDocument.Parse("""{"url":"data:text/html,<title>Test Page</title><h1>Hello</h1>"}""");
        await client.SendAsync("Page.navigate", navParams.RootElement.Clone());

        using var evalParams = JsonDocument.Parse("""{"expression":"document.title","returnByValue":true}""");
        string? title = null;
        await AsyncTest.EventuallyAsync(async () =>
        {
            var result = await client.SendAsync("Runtime.evaluate", evalParams.RootElement.Clone());
            title = result.GetProperty("result").GetProperty("value").GetString();
            return title == "Test Page";
        }, TimeSpan.FromSeconds(5), "navigated page title was not observable");
        Assert.Equal("Test Page", title);
    }

    [ExternalFact(TestOperatingSystem.MacOS)]
    public async Task Subscribe_ReceivesConsoleEvent()
    {
        await using var client = new CdpClient(_wsUrl);

        using var enableParams = JsonDocument.Parse("{}");
        await client.SendAsync("Runtime.enable", enableParams.RootElement.Clone());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var events = client.SubscribeAsync("Runtime.consoleAPICalled", cts.Token).GetAsyncEnumerator(cts.Token);
        var eventTask = events.MoveNextAsync().AsTask();
        using var navParams = JsonDocument.Parse("""{"url":"data:text/html,<script>console.log('hello-cdp')</script>"}""");
        await client.SendAsync("Page.navigate", navParams.RootElement.Clone());

        Assert.True(await eventTask.WaitAsync(TimeSpan.FromSeconds(5)));
        var receivedText = events.Current.Params.GetProperty("args")[0].GetProperty("value").GetString();
        Assert.Equal("hello-cdp", receivedText);
    }
}
