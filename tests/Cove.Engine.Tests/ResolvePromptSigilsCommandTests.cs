using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Cove.Engine;
using Cove.Engine.Skills;
using Cove.Protocol;
using Xunit;

public class ResolvePromptSigilsCommandTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-sigilcmd-" + Guid.NewGuid().ToString("N"));

    private static void WriteSkill(string dir, string name, string description)
    {
        var skillDir = Path.Combine(dir, name);
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), $$"""
        ---
        name: {{name}}
        description: {{description}}
        ---
        # {{name}} body
        """);
    }

    [Fact]
    public async Task ResolvePromptSigils_ReturnsResolvedAndUnresolved()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "skills"));
        WriteSkill(Path.Combine(dir, "skills"), "findable", "a skill");
        try
        {
            using var skills = new SkillsService(dir, includeHarnessRoots: false);
            var prm = JsonSerializer.SerializeToElement(new { prompt = "use +findable and +missing" });
            var request = new ControlRequest("1", "cove://commands/skills.resolve-prompt-sigils", prm);
            var response = await EngineCommandRouter.RouteAsync(request, skills: skills);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var data = response.Data!.Value;
            var resolved = data.GetProperty("resolved");
            Assert.Equal(1, resolved.GetArrayLength());
            Assert.Equal("findable", resolved[0].GetProperty("name").GetString());
            var unresolved = data.GetProperty("unresolved");
            Assert.Equal(1, unresolved.GetArrayLength());
            Assert.Equal("missing", unresolved[0].GetString());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ResolvePromptSigils_EmptyPrompt_ReturnsError()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            using var skills = new SkillsService(dir, includeHarnessRoots: false);
            var prm = JsonSerializer.SerializeToElement(new { prompt = "" });
            var request = new ControlRequest("1", "cove://commands/skills.resolve-prompt-sigils", prm);
            var response = await EngineCommandRouter.RouteAsync(request, skills: skills);

            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("invalid_params", response.Error!.Code);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ResolvePromptSigils_IgnoresSigilsInCodeFences()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "skills"));
        WriteSkill(Path.Combine(dir, "skills"), "fenced", "a skill");
        try
        {
            using var skills = new SkillsService(dir, includeHarnessRoots: false);
            var prm = JsonSerializer.SerializeToElement(new { prompt = "```\n+fenced\n```\n+fenced" });
            var request = new ControlRequest("1", "cove://commands/skills.resolve-prompt-sigils", prm);
            var response = await EngineCommandRouter.RouteAsync(request, skills: skills);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var resolved = response.Data!.Value.GetProperty("resolved");
            Assert.Equal(1, resolved.GetArrayLength());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
