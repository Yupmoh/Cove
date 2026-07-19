using System.Text.Json;
using Cove.Engine.Lsp;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LspServerLiveTests : IAsyncLifetime
{
    private string? _serverPath;
    private string _workDir = "";

    private string[] _serverArgs = Array.Empty<string>();

    public Task InitializeAsync()
    {
        var nativeTs = FindNativeTypeScriptLsp();
        if (nativeTs is not null)
        {
            _serverPath = nativeTs;
            _serverArgs = new[] { "--lsp", "--stdio" };
        }
        else
        {
            _serverPath = FindServerOnPath("typescript-language-server");
            _serverArgs = new[] { "--stdio" };
        }
        Assert.NotNull(_serverPath);
        _workDir = TestDirectory.Create("cove-lsp-live");
        return Task.CompletedTask;
    }

    private static string? FindNativeTypeScriptLsp()
    {
        var platform = OperatingSystem.IsMacOS()
            ? (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "darwin-arm64" : "darwin-x64")
            : OperatingSystem.IsLinux() ? "linux-x64" : "win-x64";
        var roots = new[] { "/opt/homebrew/lib/node_modules", "/usr/local/lib/node_modules", "/usr/lib/node_modules" };
        foreach (var root in roots)
        {
            var candidate = Path.Combine(root, "typescript", "node_modules", "@typescript", "typescript-" + platform, "lib", "tsc");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public Task DisposeAsync()
    {
        TestDirectory.Delete(_workDir);
        return Task.CompletedTask;
    }

    private static string? FindServerOnPath(string name)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        foreach (var dir in paths)
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private LspServer CreateServer()
    {
        var config = new LspServerConfig(_serverPath!, _serverArgs, new[] { "typescript" }, RootUri: ToUri(_workDir));
        return new LspServer(config, NullLogger.Instance);
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_workDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string ToUri(string path) => "file://" + path;

    private async Task OpenDocumentAsync(LspServer server, string path, string content, CancellationToken ct)
    {
        var didOpen = Parse($$$"""
        {"textDocument":{"uri":"{{{ToUri(path)}}}","languageId":"typescript","version":1,"text":{{{JsonSerializer.Serialize(content)}}}}}
        """);
        await server.SendNotificationAsync("textDocument/didOpen", didOpen, ct);
    }

    [LiveFact]
    public async Task Initialize_RealServer_Succeeds()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var server = CreateServer();

        var started = await server.StartAsync(cts.Token);

        Assert.True(started);
        Assert.True(server.IsRunning);
    }

    [LiveFact]
    public async Task PullDiagnostics_TypeError_ReportsAssignabilityError()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var server = CreateServer();
        Assert.True(await server.StartAsync(cts.Token));

        var content = "const x: number = \"not a number\";\n";
        var path = WriteFile("broken.ts", content);
        await OpenDocumentAsync(server, path, content, cts.Token);

        var diagParams = Parse($$$"""
        {"textDocument":{"uri":"{{{ToUri(path)}}}"}}
        """);
        var result = await server.SendRequestAsync("textDocument/diagnostic", diagParams, cts.Token);

        Assert.NotNull(result);
        var items = result.Value.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);
        var first = items[0];
        Assert.Contains("not assignable", first.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2322, first.GetProperty("code").GetInt32());
    }

    [LiveFact]
    public async Task Hover_OverTypedConst_ReturnsContents()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var server = CreateServer();
        Assert.True(await server.StartAsync(cts.Token));

        var content = "const greeting: string = \"hello\";\nconsole.log(greeting);\n";
        var path = WriteFile("hover.ts", content);
        await OpenDocumentAsync(server, path, content, cts.Token);

        var hoverParams = Parse($$$"""
        {"textDocument":{"uri":"{{{ToUri(path)}}}"},"position":{"line":1,"character":13}}
        """);
        var result = await server.SendRequestAsync("textDocument/hover", hoverParams, cts.Token);

        Assert.NotNull(result);
        Assert.True(result.Value.TryGetProperty("contents", out var contents));
        Assert.Contains("string", contents.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [LiveFact]
    public async Task Definition_AtCallSite_ReturnsDeclarationLocation()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var server = CreateServer();
        Assert.True(await server.StartAsync(cts.Token));

        var content = "function add(a: number, b: number): number { return a + b; }\nconst sum = add(1, 2);\n";
        var path = WriteFile("def.ts", content);
        await OpenDocumentAsync(server, path, content, cts.Token);

        var defParams = Parse($$$"""
        {"textDocument":{"uri":"{{{ToUri(path)}}}"},"position":{"line":1,"character":12}}
        """);
        var result = await server.SendRequestAsync("textDocument/definition", defParams, cts.Token);

        Assert.NotNull(result);
        var raw = result.Value.GetRawText();
        Assert.Contains("def.ts", raw, StringComparison.Ordinal);
        Assert.Contains("\"line\":0", raw.Replace(" ", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }
}
