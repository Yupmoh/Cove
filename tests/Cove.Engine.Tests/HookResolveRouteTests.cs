using System.Text;
using System.Text.Json;
using Cove.Adapters;
using Cove.Engine.Hooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HookResolveRouteTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-resolve-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Resolve_Identity_ReturnsRawContext()
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
            msg.Content = new StringContent("\"my context\"", Encoding.UTF8, "application/json");
            var resp = await client.SendAsync(msg);

            Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Equal("my context", body);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Resolve_HookSpecificOutput_ReturnsEnvelope()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            using var server = new HookHttpServer(dir);
            var matrix = new HookEnvelopeMatrix();
            matrix.Register("claude-code", "userPromptSubmit", HookEnvelopeKind.HookSpecificOutput);
            server.Injector = new ContextInjector(matrix);
            await server.StartAsync();

            using var client = new HttpClient();
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{server.Port}/resolve");
            msg.Headers.Add("X-Cove-Adapter", "claude-code");
            msg.Headers.Add("X-Cove-Event", "userPromptSubmit");
            msg.Content = new StringContent("\"injected text\"", Encoding.UTF8, "application/json");
            var resp = await client.SendAsync(msg);

            Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("hookSpecificOutput", body);
            Assert.Contains("additionalContext", body);
            Assert.Contains("injected text", body);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Resolve_WithoutInjector_Returns400()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            using var server = new HookHttpServer(dir);
            await server.StartAsync();

            using var client = new HttpClient();
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{server.Port}/resolve");
            msg.Headers.Add("X-Cove-Adapter", "claude-code");
            msg.Headers.Add("X-Cove-Event", "sessionStartManifest");
            msg.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            var resp = await client.SendAsync(msg);

            Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }
}
