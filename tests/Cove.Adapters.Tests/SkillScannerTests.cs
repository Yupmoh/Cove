using Cove.Adapters;
using Cove.Testing;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class SkillScannerTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-skills-" + Guid.NewGuid().ToString("N"));

    private static void WriteSkill(string dir, string name, string description = "test skill", string? body = null)
    {
        var skillDir = Path.Combine(dir, name);
        Directory.CreateDirectory(skillDir);
        var content = $$"""
        ---
        name: {{name}}
        description: {{description}}
        ---
        {{body ?? "# " + name}}
        """;
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), content);
    }

    [Fact]
    public void Scan_FindsSkillsInRoot()
    {
        var dir = NewDir();
        try
        {
            WriteSkill(dir, "my-skill", "does the thing");
            var scanner = new SkillScanner();
            var skills = scanner.ScanRoot(dir, SkillSource.CoveUser);

            Assert.Single(skills);
            Assert.Equal("my-skill", skills[0].Name);
            Assert.Equal("does the thing", skills[0].Description);
            Assert.Equal(SkillSource.CoveUser, skills[0].Source);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Scan_DepthCappedAtTwo()
    {
        var dir = NewDir();
        try
        {
            WriteSkill(dir, "shallow");
            Directory.CreateDirectory(Path.Combine(dir, "sub"));
            WriteSkill(Path.Combine(dir, "sub"), "nested");
            Directory.CreateDirectory(Path.Combine(dir, "deep", "very", "level"));
            WriteSkill(Path.Combine(dir, "deep", "very", "level"), "too-deep");

            var scanner = new SkillScanner();
            var skills = scanner.ScanRoot(dir, SkillSource.CoveUser);

            Assert.Equal(2, skills.Count);
            Assert.Contains(skills, s => s.Name == "shallow");
            Assert.Contains(skills, s => s.Name == "nested");
            Assert.DoesNotContain(skills, s => s.Name == "too-deep");
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Scan_SetsProvenanceForHarnessSource()
    {
        var dir = NewDir();
        try
        {
            WriteSkill(dir, "harness-skill");
            var scanner = new SkillScanner();
            var skills = scanner.ScanRoot(dir, SkillSource.Harness, adapterName: "claude-code");

            Assert.Single(skills);
            Assert.Equal("harness:claude-code", skills[0].Provenance);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Scan_SkipsInvalidFrontmatter_Gracefully()
    {
        var dir = NewDir();
        try
        {
            WriteSkill(dir, "good-skill");
            var badDir = Path.Combine(dir, "bad-skill");
            Directory.CreateDirectory(badDir);
            File.WriteAllText(Path.Combine(badDir, "SKILL.md"), "no frontmatter here");

            var scanner = new SkillScanner();
            var skills = scanner.ScanRoot(dir, SkillSource.CoveUser);

            Assert.Single(skills);
            Assert.Equal("good-skill", skills[0].Name);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Scan_SkipsMissingSkillMd()
    {
        var dir = NewDir();
        try
        {
            WriteSkill(dir, "present");
            Directory.CreateDirectory(Path.Combine(dir, "no-skill-md"));
            var scanner = new SkillScanner();
            var skills = scanner.ScanRoot(dir, SkillSource.CoveUser);

            Assert.Single(skills);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Scan_RejectsDescriptionOverLimit()
    {
        var dir = NewDir();
        try
        {
            var longDesc = new string('x', 1025);
            WriteSkill(dir, "verbose-skill", longDesc);
            var scanner = new SkillScanner();
            var skills = scanner.ScanRoot(dir, SkillSource.CoveUser);

            Assert.Empty(skills);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Index_AggregatesMultipleSources_FirstMatchWins()
    {
        var dir1 = NewDir();
        var dir2 = NewDir();
        try
        {
            WriteSkill(dir1, "shared", "from cove");
            WriteSkill(dir2, "shared", "from harness");
            WriteSkill(dir2, "harness-only", "harness specific");

            var index = new SkillIndex();
            index.AddRoot(dir1, SkillSource.CoveUser);
            index.AddRoot(dir2, SkillSource.Harness, adapterName: "claude-code");
            index.Rebuild();

            var skills = index.List();
            Assert.Equal(2, skills.Count);
            var shared = skills.First(s => s.Name == "shared");
            Assert.Equal(SkillSource.CoveUser, shared.Source);
            Assert.Equal("from cove", shared.Description);
        }
        finally { TestDirectory.Delete(dir1); TestDirectory.Delete(dir2); }
    }

    [Fact]
    public void Index_Search_FiltersByNameAndDescription()
    {
        var dir = NewDir();
        try
        {
            WriteSkill(dir, "git-helper", "manages git operations");
            WriteSkill(dir, "docker-tools", "container management");
            var index = new SkillIndex();
            index.AddRoot(dir, SkillSource.CoveUser);
            index.Rebuild();

            var results = index.Search("git");
            Assert.Single(results);
            Assert.Equal("git-helper", results[0].Name);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Index_ResolveByName_ReturnsSkill()
    {
        var dir = NewDir();
        try
        {
            WriteSkill(dir, "findable", "easy to find");
            var index = new SkillIndex();
            index.AddRoot(dir, SkillSource.CoveUser);
            index.Rebuild();

            var skill = index.Resolve("findable");
            Assert.NotNull(skill);
            Assert.Equal("findable", skill!.Name);
        }
        finally { TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Index_Resolve_UnkownName_ReturnsNull()
    {
        var index = new SkillIndex();
        index.Rebuild();

        Assert.Null(index.Resolve("nonexistent"));
    }
}
