using System.Text.Json;
using Cove.Engine.Hooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AmbientContextProviderTests
{
    [Fact]
    public void SessionStartManifest_Assembles_Primer_Skills_Agent()
    {
        var provider = new SessionStartContextProvider(
            primer: "cove-context.md content",
            skillsManifest: "{\"skills\":[]}",
            agentPackaging: "{\"agent\":\"claude\"}");
        var context = provider.Build();
        var json = JsonDocument.Parse(context.GetRawText());
        Assert.Equal("cove-context.md content", json.RootElement.GetProperty("context").GetString());
        Assert.True(json.RootElement.GetProperty("skills").GetBoolean());
        Assert.True(json.RootElement.GetProperty("agent").GetBoolean());
    }

    [Fact]
    public void LocationContext_IncludesRoom_Wing_Workspace_Panes()
    {
        var provider = new LocationContextProvider(
            room: "main",
            wing: "left",
            workspace: "default",
            otherPanes: new[] { "agent-2", "agent-3" });
        var context = provider.Build();
        var json = JsonDocument.Parse(context.GetRawText());
        Assert.Equal("main", json.RootElement.GetProperty("room").GetString());
        Assert.Equal("left", json.RootElement.GetProperty("wing").GetString());
        Assert.Equal("default", json.RootElement.GetProperty("workspace").GetString());
        var panes = json.RootElement.GetProperty("panes").EnumerateArray().Select(p => p.GetString()).ToArray();
        Assert.Equal(new[] { "agent-2", "agent-3" }, panes);
    }

    [Fact]
    public void LocationContext_NullWing_OmitsWing()
    {
        var provider = new LocationContextProvider(
            room: "main",
            wing: null,
            workspace: "default",
            otherPanes: Array.Empty<string?>());
        var context = provider.Build();
        var json = JsonDocument.Parse(context.GetRawText());
        Assert.False(json.RootElement.TryGetProperty("wing", out _));
    }

    [Fact]
    public void RunCommandNudge_NoRunningCommands_Empty()
    {
        var provider = new RunCommandContextProvider(runningCommands: Array.Empty<string>());
        var context = provider.Build();
        Assert.Equal("{}", context.GetRawText());
    }

    [Fact]
    public void RunCommandNudge_WithRunningCommands_ListsThem()
    {
        var provider = new RunCommandContextProvider(runningCommands: new[] { "npm dev", "cargo watch" });
        var context = provider.Build();
        var json = JsonDocument.Parse(context.GetRawText());
        var cmds = json.RootElement.GetProperty("runningCommands").EnumerateArray().Select(c => c.GetString()).ToArray();
        Assert.Equal(new[] { "npm dev", "cargo watch" }, cmds);
    }

    [Fact]
    public void AmbientContextAggregator_CombinesProviders()
    {
        var aggregator = new AmbientContextAggregator();
        aggregator.Add("session", new SessionStartContextProvider("primer", "{}", "{}"));
        aggregator.Add("location", new LocationContextProvider("room", null, "ws", Array.Empty<string?>()));

        var session = aggregator.Get("session");
        var location = aggregator.Get("location");

        Assert.NotNull(session);
        Assert.NotNull(location);
        Assert.Null(aggregator.Get("nonexistent"));
    }

    [Fact]
    public void AmbientContextAggregator_Remove_RemovesProvider()
    {
        var aggregator = new AmbientContextAggregator();
        aggregator.Add("session", new SessionStartContextProvider("primer", "{}", "{}"));
        aggregator.Remove("session");
        Assert.Null(aggregator.Get("session"));
    }
}
