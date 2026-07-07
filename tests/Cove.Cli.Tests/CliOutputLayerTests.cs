using Cove.Cli;
using Xunit;

namespace Cove.Cli.Tests;

public sealed class CliOutputLayerTests
{
    private static CommandContext NewContext(StringWriter stdout, StringWriter stderr, string[] args)
    {
        var tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-cli-test-" + System.Guid.NewGuid().ToString("N"));
        var prev = System.Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        System.Environment.SetEnvironmentVariable("COVE_DATA_DIR", tempRoot);
        try
        {
            var dataDir = Cove.Platform.CoveDataDir.Resolve(Cove.Platform.CoveChannel.Stable);
            var paths = new Cove.Engine.Daemon.DaemonPaths(dataDir);
            var endpoint = Cove.Platform.Ipc.ControlEndpointFactory.FromSocketPath(System.IO.Path.Combine(tempRoot, "test.sock"));
            return new CommandContext(paths, endpoint, stdout, stderr, args);
        }
        finally { System.Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev); }
    }

    [Fact]
    public void IsJson_TrueWhenDashDashJsonPresent()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), new[] { "--json" });
        Assert.True(ctx.IsJson);
    }

    [Fact]
    public void IsJson_FalseWhenAbsent()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), System.Array.Empty<string>());
        Assert.False(ctx.IsJson);
    }

    [Fact]
    public void Filter_ExtractsColumnValuePair()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), new[] { "--filter", "name=foo" });
        Assert.Equal("name=foo", ctx.Filter);
    }

    [Fact]
    public void Filter_NullWhenAbsent()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), System.Array.Empty<string>());
        Assert.Null(ctx.Filter);
    }

    [Fact]
    public void Source_DefaultsToUserCli()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), System.Array.Empty<string>());
        Assert.Equal("user:cli", ctx.Source);
    }

    [Fact]
    public void Source_OverriddenByDashDashSource()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), new[] { "--source", "agent:p1" });
        Assert.Equal("agent:p1", ctx.Source);
    }

    [Fact]
    public void Channel_DefaultsStable()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), System.Array.Empty<string>());
        Assert.Equal(Cove.Platform.CoveChannel.Stable, ctx.Channel);
    }

    [Fact]
    public void Channel_OverriddenByDashDashChannel()
    {
        var ctx = NewContext(new StringWriter(), new StringWriter(), new[] { "--channel", "dev" });
        Assert.Equal(Cove.Platform.CoveChannel.Dev, ctx.Channel);
    }
    [Fact]
    public void RenderJson_WritesDataToStdout()
    {
        var stdout = new StringWriter();
        var ctx = NewContext(stdout, new StringWriter(), new[] { "--json" });
        var data = System.Text.Json.JsonSerializer.SerializeToElement(new { name = "test", count = 5 });
        ctx.Render(data);
        var output = stdout.ToString().Trim();
        Assert.Contains("\"name\"", output);
        Assert.Contains("\"count\"", output);
        Assert.Equal("test", System.Text.Json.JsonDocument.Parse(output).RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void RenderStatus_WritesToStderrNotStdout()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var ctx = NewContext(stdout, stderr, System.Array.Empty<string>());
        ctx.RenderStatus("operation complete");
        Assert.Equal("", stdout.ToString());
        Assert.Contains("operation complete", stderr.ToString());
    }
}
