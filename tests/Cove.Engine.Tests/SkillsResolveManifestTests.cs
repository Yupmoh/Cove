using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Cove.Engine.Skills;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SkillsResolveManifestTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-skillrm-" + Guid.NewGuid().ToString("N"));

    private static void WriteSkill(string dir, string name)
    {
        var skillDir = Path.Combine(dir, name);
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), $$"""
        ---
        name: {{name}}
        description: test skill
        ---
        # {{name}}
        body content
        """);
    }

    [Fact]
    public async Task ResolveManifest_PrefixSkillName_ReturnsNotFound()
    {
        var dir = NewDir();
        Directory.CreateDirectory(Path.Combine(dir, "skills"));
        WriteSkill(Path.Combine(dir, "skills"), "my-skill-full-name");
        try
        {
            using var skills = new SkillsService(dir, includeHarnessRoots: false);
            var prm = JsonSerializer.SerializeToElement(new SkillsResolveManifestParams("my-skill"), CoveJsonContext.Default.SkillsResolveManifestParams);
            var request = new ControlRequest("1", "cove://commands/skills.resolve-manifest", prm);

            var response = await EngineCommandRouter.RouteAsync(request, skills: skills);

            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("not_found", response.Error!.Code);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task ResolveManifest_ExactName_ReturnsManifest()
    {
        var dir = NewDir();
        Directory.CreateDirectory(Path.Combine(dir, "skills"));
        WriteSkill(Path.Combine(dir, "skills"), "my-skill");
        try
        {
            using var skills = new SkillsService(dir, includeHarnessRoots: false);
            var prm = JsonSerializer.SerializeToElement(new SkillsResolveManifestParams("my-skill"), CoveJsonContext.Default.SkillsResolveManifestParams);
            var request = new ControlRequest("1", "cove://commands/skills.resolve-manifest", prm);

            var response = await EngineCommandRouter.RouteAsync(request, skills: skills);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            Assert.Equal("my-skill", response.Data!.Value.GetProperty("name").GetString());
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }
}
