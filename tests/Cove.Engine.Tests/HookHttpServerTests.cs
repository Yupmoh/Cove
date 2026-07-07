using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cove.Adapters;
using Cove.Engine;
using Cove.Engine.Hooks;
using Cove.Protocol;
using Xunit;

public class HookHttpServerTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-hook-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Start_WritesPortToFile()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var portFile = Path.Combine(dir, "hook-port");
            using var server = new HookHttpServer(dir);
            await server.StartAsync();

            Assert.True(File.Exists(portFile));
            var port = int.Parse(File.ReadAllText(portFile).Trim());
            Assert.True(port > 0);
            Assert.Equal(port, server.Port);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task EmitEndpoint_AcceptsPost_RoutesEvent()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            using var server = new HookHttpServer(dir);
            await server.StartAsync();

            HookEvent? received = null;
            server.OnEvent += e => received = e;

            using var client = new HttpClient();
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"http://127.0.0.1:{server.Port}/api/adapter/claude-code/session-start", content);

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.NotNull(received);
            Assert.Equal("claude-code", received!.Adapter);
            Assert.Equal("session-start", received.Event);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task EmitEndpoint_ExtractsPaneIdHeader()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            using var server = new HookHttpServer(dir);
            await server.StartAsync();

            HookEvent? received = null;
            server.OnEvent += e => received = e;

            using var client = new HttpClient();
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{server.Port}/api/adapter/codex/stop");
            msg.Headers.Add("X-Cove-Pane-Id", "pane-42");
            msg.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            var resp = await client.SendAsync(msg);

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.NotNull(received);
            Assert.Equal("pane-42", received!.PaneId);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ContextInjectionRoute_ReturnsCoveContext()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            using var server = new HookHttpServer(dir);
            server.SetContext(JsonDocument.Parse("""{"workspace":"my-ws","paneId":"p1"}""").RootElement.Clone());
            await server.StartAsync();

            using var client = new HttpClient();
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{server.Port}/api/adapter/claude-code/session-start");
            msg.Headers.Add("X-Cove-Pane-Id", "p1");
            msg.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            var resp = await client.SendAsync(msg);

            Assert.True(resp.Headers.Contains("X-Cove-Pane-Id"));
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("coveContext", body);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task StateReadback_ReturnsServerInfo()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            using var server = new HookHttpServer(dir);
            await server.StartAsync();

            var request = new ControlRequest("1", "cove://hooks/_state");
            var response = await EngineCommandRouter.RouteAsync(request, hookServer: server);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var data = response.Data!.Value;
            Assert.Equal(server.Port, data.GetProperty("port").GetInt32());
            Assert.True(data.GetProperty("running").GetBoolean());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
