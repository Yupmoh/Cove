using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Cove.Adapters;
using Cove.Engine;
using Cove.Protocol;
using Xunit;

public class AgentDefinitionCommandTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-agentcmd-" + Guid.NewGuid().ToString("N"));

    private static AgentDefinitionStore MakeStore(string dir)
    {
        Directory.CreateDirectory(dir);
        return new AgentDefinitionStore(dir);
    }

    [Fact]
    public async Task List_ReturnsAllAgents()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            store.Save(new AgentDefinition("agent-one", "One", "desc1", "claude-code", "prompt1", new List<string>()));
            store.Save(new AgentDefinition("agent-two", "Two", "desc2", "codex", "prompt2", new List<string> { "skill-a" }));

            var request = new ControlRequest("1", "cove://commands/agent.definition.list");
            var response = await EngineCommandRouter.RouteAsync(request, agents: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var agents = response.Data!.Value.GetProperty("agents");
            Assert.Equal(2, agents.GetArrayLength());
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Show_ReturnsAgentDetails()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            store.Save(new AgentDefinition("showable", "Showable", "a test", "claude-code", "prompt body", new List<string> { "skill1" }));

            var prm = JsonSerializer.SerializeToElement(new { slug = "showable" });
            var request = new ControlRequest("1", "cove://commands/agent.definition.show", prm);
            var response = await EngineCommandRouter.RouteAsync(request, agents: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var data = response.Data!.Value;
            Assert.Equal("showable", data.GetProperty("slug").GetString());
            Assert.Equal("prompt body", data.GetProperty("prompt").GetString());
            Assert.Equal(1, data.GetProperty("attachedSkills").GetArrayLength());
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Show_UnknownSlug_ReturnsNotFound()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            var prm = JsonSerializer.SerializeToElement(new { slug = "nonexistent" });
            var request = new ControlRequest("1", "cove://commands/agent.definition.show", prm);
            var response = await EngineCommandRouter.RouteAsync(request, agents: store);

            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("not_found", response.Error!.Code);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Delete_RemovesAgent()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            store.Save(new AgentDefinition("deletable", "Del", "d", "claude-code", "p", new List<string>()));

            var prm = JsonSerializer.SerializeToElement(new { slug = "deletable" });
            var request = new ControlRequest("1", "cove://commands/agent.definition.delete", prm);
            var response = await EngineCommandRouter.RouteAsync(request, agents: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            Assert.Null(store.Load("deletable"));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Delete_InvalidSlug_ReturnsInvalidParams()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            var prm = JsonSerializer.SerializeToElement(new { slug = "../escape" });
            var request = new ControlRequest("1", "cove://commands/agent.definition.delete", prm);
            var response = await EngineCommandRouter.RouteAsync(request, agents: store);

            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("invalid_params", response.Error!.Code);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task List_WithoutStore_ReturnsNotReady()
    {
        var request = new ControlRequest("1", "cove://commands/agent.definition.list");
        var response = await EngineCommandRouter.RouteAsync(request);

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("not_ready", response.Error!.Code);
    }
}
