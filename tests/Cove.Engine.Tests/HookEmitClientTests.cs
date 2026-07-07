using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Cove.Engine.Hooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HookEmitClientTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-emit-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Emit_PostsToServer_ReturnsResponse()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            using var server = new HookHttpServer(dir);
            await server.StartAsync();

            var client = new HookEmitClient(server.Port);
            var result = await client.EmitAsync("claude-code", "session-start", "{}");

            Assert.True(result.Ok);
            Assert.Equal(200, result.StatusCode);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Emit_WithPaneId_SendsHeader()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            using var server = new HookHttpServer(dir);
            string? receivedPaneId = null;
            server.OnEvent += e => receivedPaneId = e.PaneId;
            await server.StartAsync();

            var client = new HookEmitClient(server.Port);
            var result = await client.EmitAsync("codex", "stop", "{}", paneId: "pane-99");

            Assert.True(result.Ok);
            Assert.Equal("pane-99", receivedPaneId);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Emit_ServerDown_ReturnsFailure()
    {
        var client = new HookEmitClient(1);
        var result = await client.EmitAsync("claude-code", "session-start", "{}");
        Assert.False(result.Ok);
    }
}
