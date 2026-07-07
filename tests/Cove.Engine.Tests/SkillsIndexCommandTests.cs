using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Cove.Engine;
using Cove.Engine.Skills;
using Cove.Protocol;
using Xunit;

public class SkillsIndexCommandTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-skillcmd-" + Guid.NewGuid().ToString("N"));

    private static void WriteSkill(string dir, string name, string description)
    {
        var skillDir = Path.Combine(dir, name);
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), $$"""
        ---
        name: {{name}}
        description: {{description}}
        ---
        # {{name}}
        """);
    }

    [Fact]
    public async Task SkillsIndex_ReturnsListedSkills()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "skills"));
        WriteSkill(Path.Combine(dir, "skills"), "test-skill", "a test skill");
        try
        {
            using var skills = new SkillsService(dir, includeHarnessRoots: false);
            var request = new ControlRequest("1", "cove://commands/skills.index");
            var response = await EngineCommandRouter.RouteAsync(request, skills: skills);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var data = response.Data!.Value;
            var skillsArray = data.GetProperty("skills");
            Assert.Equal(1, skillsArray.GetArrayLength());
            var first = skillsArray[0];
            Assert.Equal("test-skill", first.GetProperty("name").GetString());
            Assert.Equal("a test skill", first.GetProperty("description").GetString());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task SkillsIndex_WithoutService_ReturnsNotReadyError()
    {
        var request = new ControlRequest("1", "cove://commands/skills.index");
        var response = await EngineCommandRouter.RouteAsync(request);

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("not_ready", response.Error!.Code);
    }
}
