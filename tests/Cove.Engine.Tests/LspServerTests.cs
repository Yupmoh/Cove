using System.Text.Json;
using Cove.Engine.Lsp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LspServerTests : IAsyncDisposable
{
    private string _mockServerPath = null!;

    public LspServerTests()
    {
        _mockServerPath = CreateMockServer();
    }

    private static string CreateMockServer()
    {
        var script = """
import sys, json

def read_message():
    headers = {}
    while True:
        line = sys.stdin.buffer.readline()
        if not line:
            return None
        line = line.decode('ascii').strip()
        if not line:
            break
        if ':' in line:
            k, v = line.split(':', 1)
            headers[k.strip().lower()] = v.strip()
    length = int(headers.get('content-length', '0'))
    body = sys.stdin.buffer.read(length)
    return json.loads(body)

def write_message(msg):
    body = json.dumps(msg).encode('utf-8')
    header = f"Content-Length: {len(body)}\r\n\r\n".encode('ascii')
    sys.stdout.buffer.write(header)
    sys.stdout.buffer.write(body)
    sys.stdout.buffer.flush()

while True:
    msg = read_message()
    if msg is None:
        break
    if msg.get('method') == 'initialize':
        write_message({
            'jsonrpc': '2.0',
            'id': msg['id'],
            'result': {'capabilities': {'textDocumentSync': 1}}
        })
    elif msg.get('method') == 'initialized':
        pass
    elif 'id' in msg:
        write_message({
            'jsonrpc': '2.0',
            'id': msg['id'],
            'result': {}
        })
    elif msg.get('method') == 'textDocument/publishDiagnostics':
        pass
    elif msg.get('method') == 'textDocument/didOpen':
        write_message({
            'jsonrpc': '2.0',
            'method': 'textDocument/publishDiagnostics',
            'params': {'uri': 'file:///test.ts', 'diagnostics': [{'range': {'start': {'line': 0, 'character': 0}, 'end': {'line': 0, 'character': 5}}, 'severity': 1, 'message': 'test diagnostic'}]}
        })
""";
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-lsp-mock-{System.Guid.NewGuid():N}.py");
        System.IO.File.WriteAllText(path, script);
        return path;
    }

    private LspServer NewServer()
    {
        var config = new LspServerConfig("python3", [_mockServerPath], ["typescript"]);
        return new LspServer(config, NullLogger.Instance);
    }

    [Fact]
    public async Task StartAsync_InitializesServer()
    {
        await using var server = NewServer();
        var started = await server.StartAsync();
        Assert.True(started);
        Assert.True(server.IsRunning);
    }

    [Fact]
    public async Task SendRequestAsync_ReturnsResult()
    {
        await using var server = NewServer();
        await server.StartAsync();

        using var doc1 = System.Text.Json.JsonDocument.Parse("""{"textDocument":{"uri":"file:///test.ts"}}""");
        var result = await server.SendRequestAsync("textDocument/documentSymbol", doc1.RootElement.Clone());

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SendNotificationAsync_DoesNotHang()
    {
        await using var server = NewServer();
        await server.StartAsync();
        using var doc2 = System.Text.Json.JsonDocument.Parse("""{"textDocument":{"uri":"file:///test.ts","languageId":"typescript","version":1,"text":"const x = 1;"}}""");
        await server.SendNotificationAsync("textDocument/didOpen", doc2.RootElement.Clone());

        Assert.True(server.IsRunning);
    }

    [Fact]
    public async Task StartAsync_InvalidCommand_ReturnsFalse()
    {
        var config = new LspServerConfig("/nonexistent/binary", [], ["typescript"]);
        await using var server = new LspServer(config, NullLogger.Instance);
        var started = await server.StartAsync();
        Assert.False(started);
    }

    [Fact(Timeout = 10000)]
    public async Task Notifications_ReceivedFromServer()
    {
        await using var server = NewServer();
        await server.StartAsync();

        using var doc3 = System.Text.Json.JsonDocument.Parse("""{"textDocument":{"uri":"file:///test.ts","languageId":"typescript","version":1,"text":"const x = 1;"}}""");
        await server.SendNotificationAsync("textDocument/didOpen", doc3.RootElement.Clone());

        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
        var enumerator = server.Notifications.GetAsyncEnumerator(cts.Token);
        var received = await enumerator.MoveNextAsync();
        Assert.True(received);
        Assert.Equal("textDocument/publishDiagnostics", enumerator.Current.Method);
    }

    [Fact]
    public async Task Dispose_StopsServer()
    {
        var server = NewServer();
        await server.StartAsync();
        Assert.True(server.IsRunning);

        await server.DisposeAsync();
        Assert.False(server.IsRunning);
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Run(() =>
        {
            Cove.Testing.TestFile.Delete(_mockServerPath);
        });
    }
}
