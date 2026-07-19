using System.Text.Json;
using Cove.Cli;
using Xunit;

namespace Cove.Cli.Tests;

[Collection(CliCollectionFixture.Name)]
public sealed class TaskFilterPlumbingTests
{
    private static (CommandContext ctx, System.IO.StringWriter stdout) NewCtx(params string[] args)
    {
        var tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-filter-" + System.Guid.NewGuid().ToString("N"));
        var stdout = new System.IO.StringWriter();
        var dataDir = Cove.Platform.CoveDataDir.ForRoot(Cove.Platform.CoveChannel.Stable, tempRoot);
        var paths = new Cove.Engine.Daemon.DaemonPaths(dataDir);
        var endpoint = Cove.Platform.Ipc.ControlEndpointFactory.FromSocketPath(System.IO.Path.Combine(tempRoot, "test.sock"));
        return (new CommandContext(paths, endpoint, stdout, args: args), stdout);
    }

    private static JsonElement Array(params string[] rows)
    {
        var json = "[" + string.Join(",", rows) + "]";
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public void Render_FilterStatusEqTodo_KeepsOnlyMatchingRows()
    {
        var (ctx, stdout) = NewCtx("--json", "--filter", "status=todo");
        ctx.Render(Array("""{"title":"a","status":"todo"}""", """{"title":"b","status":"done"}""", """{"title":"c","status":"todo"}"""));
        using var doc = JsonDocument.Parse(stdout.ToString());
        var items = doc.RootElement.EnumerateArray().Select(x => x.GetProperty("status").GetString()!).ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, s => Assert.Equal("todo", s));
    }

    [Fact]
    public void Render_NoFilter_KeepsAllRows()
    {
        var (ctx, stdout) = NewCtx("--json");
        ctx.Render(Array("""{"title":"a","status":"todo"}""", """{"title":"b","status":"done"}"""));
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void Render_FilterOnScalar_ReturnsScalarUnchanged()
    {
        var (ctx, stdout) = NewCtx("--json", "--filter", "echo=hello");
        var scalar = JsonDocument.Parse("""{"echo":"hello","status":"pong"}""").RootElement.Clone();
        ctx.Render(scalar);
        Assert.Contains("\"status\":\"pong\"", stdout.ToString());
    }
}
