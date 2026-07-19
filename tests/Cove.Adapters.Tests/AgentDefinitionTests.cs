using System.IO;
using System.Text.RegularExpressions;
using Cove.Adapters;
using Cove.Testing;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class AgentDefinitionTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-agent-" + Guid.NewGuid().ToString("N"));

    private static string WriteAgent(string dir, string slug, string name, string description, string adapter, string prompt, string[]? skills = null)
    {
        var agentDir = Path.Combine(dir, slug);
        Directory.CreateDirectory(agentDir);
        var frontmatter = $$"""
        ---
        slug: {{slug}}
        name: {{name}}
        description: {{description}}
        adapter: {{adapter}}
        {{(skills is null ? "" : $"attachedSkills: [{string.Join(", ", skills)}]")}}
        ---

        """;
        File.WriteAllText(Path.Combine(agentDir, "agent.md"), frontmatter + prompt);
        return agentDir;
    }

    [Fact]
    public void Validate_AcceptsValidAgent()
    {
        var agent = new AgentDefinition("my-agent", "My Agent", "does things", "claude-code", "You are helpful", new List<string>());
        var errors = AgentDefinitionValidator.Validate(agent);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RejectsInvalidSlug()
    {
        var agent = new AgentDefinition("My_Agent", "My Agent", "desc", "claude-code", "prompt", new List<string>());
        var errors = AgentDefinitionValidator.Validate(agent);
        Assert.Contains(errors, e => e.Field == "slug");
    }

    [Fact]
    public void Validate_RejectsSlugTooLong()
    {
        var slug = new string('a', 65);
        var agent = new AgentDefinition(slug, "Name", "desc", "claude-code", "prompt", new List<string>());
        var errors = AgentDefinitionValidator.Validate(agent);
        Assert.Contains(errors, e => e.Field == "slug");
    }

    [Fact]
    public void Validate_RejectsEmptyPrompt()
    {
        var agent = new AgentDefinition("valid-slug", "Name", "desc", "claude-code", "", new List<string>());
        var errors = AgentDefinitionValidator.Validate(agent);
        Assert.Contains(errors, e => e.Field == "prompt");
    }

    [Fact]
    public void Validate_RejectsMissingAdapter()
    {
        var agent = new AgentDefinition("valid-slug", "Name", "desc", "", "prompt", new List<string>());
        var errors = AgentDefinitionValidator.Validate(agent);
        Assert.Contains(errors, e => e.Field == "adapter");
    }

    [Fact]
    public void Parse_ReadsFrontmatterAndBody()
    {
        var dir = NewDir();
        try
        {
            WriteAgent(dir, "test-agent", "Test Agent", "a test", "claude-code", "You are a test agent");
            var path = Path.Combine(dir, "test-agent", "agent.md");
            var content = File.ReadAllText(path);
            var agent = AgentDefinitionParser.Parse(content);

            Assert.NotNull(agent);
            Assert.Equal("test-agent", agent!.Slug);
            Assert.Equal("Test Agent", agent.Name);
            Assert.Equal("a test", agent.Description);
            Assert.Equal("claude-code", agent.Adapter);
            Assert.Contains("You are a test agent", agent.Prompt);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Parse_ReadsAttachedSkills()
    {
        var dir = NewDir();
        try
        {
            WriteAgent(dir, "skill-agent", "Skill Agent", "has skills", "claude-code", "prompt", new[] { "skill-a", "skill-b" });
            var path = Path.Combine(dir, "skill-agent", "agent.md");
            var content = File.ReadAllText(path);
            var agent = AgentDefinitionParser.Parse(content);

            Assert.NotNull(agent);
            Assert.Equal(2, agent!.AttachedSkills.Count);
            Assert.Contains("skill-a", agent.AttachedSkills);
            Assert.Contains("skill-b", agent.AttachedSkills);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Parse_InvalidFrontmatter_ReturnsNull()
    {
        var result = AgentDefinitionParser.Parse("no frontmatter here");
        Assert.Null(result);
    }

    [Fact]
    public void Store_WritesAndReadsBack()
    {
        var dir = NewDir();
        try
        {
            var store = new AgentDefinitionStore(dir);
            var agent = new AgentDefinition("stored-agent", "Stored", "desc", "claude-code", "prompt body", new List<string> { "skill1" });
            store.Save(agent);

            var loaded = store.Load("stored-agent");
            Assert.NotNull(loaded);
            Assert.Equal("stored-agent", loaded!.Slug);
            Assert.Equal("Stored", loaded.Name);
            Assert.Contains("skill1", loaded.AttachedSkills);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Store_List_ReturnsAllAgents()
    {
        var dir = NewDir();
        try
        {
            var store = new AgentDefinitionStore(dir);
            store.Save(new AgentDefinition("agent-one", "One", "d", "claude-code", "p", new List<string>()));
            store.Save(new AgentDefinition("agent-two", "Two", "d", "codex", "p", new List<string>()));

            var list = store.List();
            Assert.Equal(2, list.Count);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Store_Delete_RemovesAgent()
    {
        var dir = NewDir();
        try
        {
            var store = new AgentDefinitionStore(dir);
            store.Save(new AgentDefinition("doomed", "Doomed", "d", "claude-code", "p", new List<string>()));
            store.Delete("doomed");

            Assert.Null(store.Load("doomed"));
        }
        finally { TestDirectory.Delete(dir); }
    }
}
