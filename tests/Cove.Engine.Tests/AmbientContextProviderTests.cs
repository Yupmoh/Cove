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
            primer: () => "cove-context.md content",
            skillsManifest: () => "{\"skills\":[]}",
            agentPackaging: () => "{\"agent\":\"claude\"}");
        var context = provider.Build(null);
        var json = JsonDocument.Parse(context.GetRawText());
        Assert.Equal("cove-context.md content", json.RootElement.GetProperty("context").GetString());
        Assert.True(json.RootElement.GetProperty("skills").GetBoolean());
        Assert.True(json.RootElement.GetProperty("agent").GetBoolean());
        Assert.Equal("{\"skills\":[]}", json.RootElement.GetProperty("skillsManifest").GetString());
        Assert.Equal("{\"agent\":\"claude\"}", json.RootElement.GetProperty("agentPackaging").GetString());
    }

    [Fact]
    public void SessionStartManifest_EmptySkills_OmitsSkillsManifest()
    {
        var provider = new SessionStartContextProvider(() => "p", () => "", () => "");
        var context = provider.Build(null);
        var json = JsonDocument.Parse(context.GetRawText());
        Assert.False(json.RootElement.GetProperty("skills").GetBoolean());
        Assert.False(json.RootElement.TryGetProperty("skillsManifest", out _));
    }

    [Fact]
    public void LocationContext_IncludesRoom_Wing_Workspace_Panes()
    {
        var provider = new LocationContextProvider(
            room: () => "main",
            wing: () => "left",
            workspace: () => "default",
            otherPanes: () => new string?[] { "agent-2", "agent-3" });
        var context = provider.Build("p1");
        var json = JsonDocument.Parse(context.GetRawText());
        Assert.Equal("main", json.RootElement.GetProperty("room").GetString());
        Assert.Equal("left", json.RootElement.GetProperty("wing").GetString());
        Assert.Equal("default", json.RootElement.GetProperty("workspace").GetString());
        Assert.Equal("p1", json.RootElement.GetProperty("paneId").GetString());
        var panes = json.RootElement.GetProperty("panes").EnumerateArray().Select(p => p.GetString()).ToArray();
        Assert.Equal(new[] { "agent-2", "agent-3" }, panes);
    }

    [Fact]
    public void LocationContext_NullRoom_OmitsRoom()
    {
        var provider = new LocationContextProvider(() => null, () => null, () => "default", () => System.Array.Empty<string?>());
        var context = provider.Build(null);
        var json = JsonDocument.Parse(context.GetRawText());
        Assert.False(json.RootElement.TryGetProperty("room", out _));
        Assert.False(json.RootElement.TryGetProperty("wing", out _));
        Assert.Equal("default", json.RootElement.GetProperty("workspace").GetString());
    }

    [Fact]
    public void RunCommandNudge_NoRunningCommands_Empty()
    {
        var provider = new RunCommandContextProvider(() => System.Array.Empty<string>());
        var context = provider.Build(null);
        Assert.Equal("{}", context.GetRawText());
    }

    [Fact]
    public void RunCommandNudge_WithRunningCommands_ListsThem()
    {
        var provider = new RunCommandContextProvider(() => new[] { "npm dev", "cargo watch" });
        var context = provider.Build(null);
        var json = JsonDocument.Parse(context.GetRawText());
        var cmds = json.RootElement.GetProperty("runningCommands").EnumerateArray().Select(c => c.GetString()).ToArray();
        Assert.Equal(new[] { "npm dev", "cargo watch" }, cmds);
    }

    [Fact]
    public void RunCommandNude_LiveQuery_ReflectsStateChange()
    {
        var running = new List<string> { "cmd1" };
        var provider = new RunCommandContextProvider(() => running);
        var first = provider.Build(null);
        running.Add("cmd2");
        var second = provider.Build(null);
        var firstCount = JsonDocument.Parse(first.GetRawText()).RootElement.GetProperty("runningCommands").GetArrayLength();
        var secondCount = JsonDocument.Parse(second.GetRawText()).RootElement.GetProperty("runningCommands").GetArrayLength();
        Assert.Equal(1, firstCount);
        Assert.Equal(2, secondCount);
    }

    [Fact]
    public void AmbientContextAggregator_CombinesProviders()
    {
        var aggregator = new AmbientContextAggregator();
        aggregator.Add("session", new SessionStartContextProvider(() => "primer", () => "{}", () => "{}"));
        aggregator.Add("location", new LocationContextProvider(() => "room", () => null, () => "ws", () => System.Array.Empty<string?>()));

        var session = aggregator.Get("session", "p1");
        var location = aggregator.Get("location", "p1");

        Assert.NotNull(session);
        Assert.NotNull(location);
        Assert.Null(aggregator.Get("nonexistent"));
    }

    [Fact]
    public void AmbientContextAggregator_Remove_RemovesProvider()
    {
        var aggregator = new AmbientContextAggregator();
        aggregator.Add("session", new SessionStartContextProvider(() => "primer", () => "{}", () => "{}"));
        aggregator.Remove("session");
        Assert.Null(aggregator.Get("session"));
    }
}
