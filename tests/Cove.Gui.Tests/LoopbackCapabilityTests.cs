using System.Net.Sockets;
using System.Text;
using Xunit;

namespace Cove.Gui.Tests;

public sealed class LoopbackCapabilityTests
{
    private const string Cap = "TESTCAP0123456789";

    private static GuiTestDirectory MakeWebRoot()
    {
        var root = GuiTestDirectory.Create("cove-loopcap-");
        root.WriteFile("index.html", "<!doctype html><title>cove</title>");
        return root;
    }

    private static LoopbackServer StartServer(string webRoot)
    {
        static Task<Stream> Dial(CancellationToken ct) => Task.FromResult<Stream>(new MemoryStream());
        var server = new LoopbackServer(webRoot, Dial, "0.0.0", "test", Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, port: 0, capability: Cap);
        return server;
    }

    private static async Task<string> RawRequestAsync(int port, string requestLine, params string[] headerLines)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);
        await using var stream = client.GetStream();
        var sb = new StringBuilder();
        sb.Append(requestLine).Append("\r\n");
        foreach (var h in headerLines)
            sb.Append(h).Append("\r\n");
        sb.Append("\r\n");
        var bytes = Encoding.ASCII.GetBytes(sb.ToString());
        await stream.WriteAsync(bytes);
        using var ms = new MemoryStream();
        var buf = new byte[4096];
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            int n;
            while ((n = await stream.ReadAsync(buf, cancellation.Token)) > 0)
            {
                ms.Write(buf, 0, n);
                if (ms.Length > 65536) break;
            }
        }
        catch (IOException exception) when (
            exception.InnerException is SocketException
            {
                SocketErrorCode: SocketError.ConnectionReset or
                    SocketError.ConnectionAborted or
                    SocketError.Shutdown
            })
        {
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        return Encoding.ASCII.GetString(ms.ToArray());
    }

    [Fact]
    public async Task Request_WithoutCapability_IsForbidden()
    {
        using var root = MakeWebRoot();
        await using var server = StartServer(root.Path);
        server.Start();
        var resp = await RawRequestAsync(server.Port, "GET / HTTP/1.1", $"Host: localhost:{server.Port}");
        Assert.Contains("403 Forbidden", resp);
    }

    [Fact]
    public async Task Request_WithCapabilityQuery_RedirectsAndSetsCookieWithoutLeakingToken()
    {
        using var root = MakeWebRoot();
        await using var server = StartServer(root.Path);
        server.Start();
        var resp = await RawRequestAsync(server.Port, $"GET /?__cap={Cap} HTTP/1.1", $"Host: localhost:{server.Port}");
        Assert.Contains("303 See Other", resp);
        Assert.Contains($"Set-Cookie: cove_cap={Cap}", resp);
        Assert.Contains("HttpOnly", resp);
        var locationLine = resp.Split("\r\n").First(l => l.StartsWith("Location:", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("__cap", locationLine);
    }

    [Fact]
    public async Task Request_ExtensionlessPageWithCapability_RedirectsPreservingOtherQuery()
    {
        using var root = MakeWebRoot();
        await using var server = StartServer(root.Path);
        server.Start();
        var resp = await RawRequestAsync(server.Port, $"GET /perf?tab=cpu&__cap={Cap} HTTP/1.1", $"Host: localhost:{server.Port}");
        Assert.Contains("303 See Other", resp);
        var locationLine = resp.Split("\r\n").First(l => l.StartsWith("Location:", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("__cap", locationLine);
        Assert.Contains("tab=cpu", locationLine);
    }

    [Fact]
    public async Task Request_WithCapabilityCookie_Serves()
    {
        using var root = MakeWebRoot();
        await using var server = StartServer(root.Path);
        server.Start();
        var resp = await RawRequestAsync(server.Port, "GET / HTTP/1.1", $"Host: localhost:{server.Port}", $"Cookie: cove_cap={Cap}");
        Assert.Contains("200 OK", resp);
    }

    [Fact]
    public async Task Request_WithoutHostHeader_IsForbidden()
    {
        using var root = MakeWebRoot();
        await using var server = StartServer(root.Path);
        server.Start();
        var resp = await RawRequestAsync(server.Port, "GET / HTTP/1.1", $"Cookie: cove_cap={Cap}");
        Assert.Contains("403 Forbidden", resp);
    }

    [Fact]
    public async Task Request_WithWrongHost_IsForbidden()
    {
        using var root = MakeWebRoot();
        await using var server = StartServer(root.Path);
        server.Start();
        var resp = await RawRequestAsync(server.Port, "GET / HTTP/1.1", "Host: evil.example.com", $"Cookie: cove_cap={Cap}");
        Assert.Contains("403 Forbidden", resp);
    }

    [Fact]
    public async Task Request_WithForeignOrigin_IsForbidden()
    {
        using var root = MakeWebRoot();
        await using var server = StartServer(root.Path);
        server.Start();
        var resp = await RawRequestAsync(server.Port, "GET / HTTP/1.1", $"Host: localhost:{server.Port}", "Origin: https://evil.example.com", $"Cookie: cove_cap={Cap}");
        Assert.Contains("403 Forbidden", resp);
    }

    [Fact]
    public async Task PtyUpgrade_WithoutCapability_IsForbidden()
    {
        using var root = MakeWebRoot();
        await using var server = StartServer(root.Path);
        server.Start();
        var resp = await RawRequestAsync(server.Port, "GET /pty?nook=abc HTTP/1.1", $"Host: localhost:{server.Port}", "Upgrade: websocket", "Connection: Upgrade", "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==");
        Assert.Contains("403 Forbidden", resp);
        Assert.DoesNotContain("101 Switching", resp);
    }
}
