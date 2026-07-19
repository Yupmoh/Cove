using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Lsp;
using Cove.Protocol;
using Cove.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LspServiceTests
{
    private static string? FindOnPath(string name)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        foreach (var dir in paths)
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string? FindBayTypeScriptLib()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "src", "Cove.Gui", "frontend", "node_modules", "typescript");
            if (File.Exists(Path.Combine(candidate, "lib", "tsserver.js"))) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent is null) break;
            dir = parent;
        }
        return null;
    }

    [ExternalFact(TestOperatingSystem.Any, "typescript-language-server")]
    public async Task Diagnostics_TypeScriptTypeError_ReportsError()
    {
        Assert.NotNull(FindOnPath("typescript-language-server"));
        var tsLib = FindBayTypeScriptLib();
        Assert.NotNull(tsLib);
        var dir = TestDirectory.Create("cove-lspsvc");
        Directory.CreateDirectory(Path.Combine(dir, "node_modules"));
        File.CreateSymbolicLink(Path.Combine(dir, "node_modules", "typescript"), tsLib!);
        try
        {
            var content = "const x: number = \"not a number\";\n";
            var filePath = Path.Combine(dir, "broken.ts");
            File.WriteAllText(filePath, content);
            await using var svc = new LspService(NullLogger.Instance);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var prm = Parse($$"""{"filePath":{{JsonSerializer.Serialize(filePath)}},"content":{{JsonSerializer.Serialize(content)}},"rootDir":{{JsonSerializer.Serialize(dir)}}}""");
            var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/lsp.diagnostics", prm), lspService: svc, cancellationToken: cts.Token);

            Assert.True(resp!.Ok);
            var data = resp.Data!.Value;
            Assert.True(data.GetProperty("available").GetBoolean());
            var diags = data.GetProperty("diagnostics");
            Assert.True(diags.GetArrayLength() > 0);
            var hasError = false;
            foreach (var d in diags.EnumerateArray())
                if (d.GetProperty("severity").GetString() == "error") hasError = true;
            Assert.True(hasError);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Diagnostics_UnknownExtension_ReturnsUnavailable()
    {
        await using var svc = new LspService(NullLogger.Instance);
        var prm = Parse("""{"filePath":"/tmp/example.zig","content":"const x = 1;","rootDir":"/tmp"}""");
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/lsp.diagnostics", prm), lspService: svc);

        Assert.True(resp!.Ok);
        var data = resp.Data!.Value;
        Assert.False(data.GetProperty("available").GetBoolean());
        Assert.False(data.TryGetProperty("language", out var lang) && lang.ValueKind == JsonValueKind.String);
        Assert.Equal(0, data.GetProperty("diagnostics").GetArrayLength());
    }

    [Fact]
    public async Task Status_ListsTypeScriptServer()
    {
        await using var svc = new LspService(NullLogger.Instance);
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/lsp.status", Parse("{}")), lspService: svc);

        Assert.True(resp!.Ok);
        var servers = resp.Data!.Value.GetProperty("servers");
        var found = false;
        foreach (var s in servers.EnumerateArray())
            if (s.GetProperty("command").GetString() == "typescript-language-server") found = true;
        Assert.True(found);
    }

    [Fact]
    public async Task Status_UserOverride_ReflectsCommand()
    {
        var userEntries = new[] { new LspConfigEntry(new[] { "typescript" }, "my-tsserver", new[] { "--stdio" }) };
        await using var svc = new LspService(NullLogger.Instance, userEntries);

        var resolved = svc.ResolveServerFor("typescript");
        Assert.NotNull(resolved);
        Assert.Equal("my-tsserver", resolved!.Command);

        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/lsp.status", Parse("{}")), lspService: svc);
        Assert.True(resp!.Ok);
        var servers = resp.Data!.Value.GetProperty("servers");
        var found = false;
        foreach (var s in servers.EnumerateArray())
            if (s.GetProperty("command").GetString() == "my-tsserver") found = true;
        Assert.True(found);
    }
}
