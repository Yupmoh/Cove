using System.Net.Http;
using System.Text;
using System.Text.Json;
using Cove.Adapters;
using Cove.Engine.Hooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AmbientContextResolveTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-ambient-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Resolve_WithoutBody_UsesAggregator()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            using var server = new HookHttpServer(dir);
            var matrix = new HookEnvelopeMatrix();
            matrix.Register("claude-code", "sessionStartManifest", HookEnvelopeKind.Identity);
            server.Injector = new ContextInjector(matrix);
            var aggregator = new AmbientContextAggregator();
            aggregator.Add("session", new SessionStartContextProvider("ambient primer", "{}", "{}"));
            server.Aggregator = aggregator;
            await server.StartAsync();

            using var client = new HttpClient();
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{server.Port}/resolve");
            msg.Headers.Add("X-Cove-Adapter", "claude-code");
            msg.Headers.Add("X-Cove-Event", "sessionStartManifest");
            var resp = await client.SendAsync(msg);

            Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(body);
            Assert.Equal("ambient primer", json.RootElement.GetProperty("context").GetString());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Resolve_WithoutBody_WithoutAggregator_ReturnsEmpty()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            using var server = new HookHttpServer(dir);
            var matrix = new HookEnvelopeMatrix();
            matrix.Register("claude-code", "sessionStartManifest", HookEnvelopeKind.Identity);
            server.Injector = new ContextInjector(matrix);
            await server.StartAsync();

            using var client = new HttpClient();
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{server.Port}/resolve");
            msg.Headers.Add("X-Cove-Adapter", "claude-code");
            msg.Headers.Add("X-Cove-Event", "sessionStartManifest");
            var resp = await client.SendAsync(msg);

            Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Equal("{}", body);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Resolve_WithBody_OverridesAggregator()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            using var server = new HookHttpServer(dir);
            var matrix = new HookEnvelopeMatrix();
            matrix.Register("claude-code", "sessionStartManifest", HookEnvelopeKind.Identity);
            server.Injector = new ContextInjector(matrix);
            var aggregator = new AmbientContextAggregator();
            aggregator.Add("session", new SessionStartContextProvider("ambient primer", "{}", "{}"));
            server.Aggregator = aggregator;
            await server.StartAsync();

            using var client = new HttpClient();
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{server.Port}/resolve");
            msg.Headers.Add("X-Cove-Adapter", "claude-code");
            msg.Headers.Add("X-Cove-Event", "sessionStartManifest");
            msg.Content = new StringContent("\"explicit body\"", Encoding.UTF8, "application/json");
            var resp = await client.SendAsync(msg);

            Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Equal("explicit body", body);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
