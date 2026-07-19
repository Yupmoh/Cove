using Cove.Adapters;
using Cove.Testing;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class SigilResolverTests
{
    private static SkillIndex MakeIndex(params (string name, string desc, SkillSource source, string? adapter)[] skills)
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-sigil-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var (name, desc, source, adapter) in skills)
        {
            var subDir = Path.Combine(dir, source.ToString());
            Directory.CreateDirectory(subDir);
            var skillDir = Path.Combine(subDir, name);
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), $$"""
            ---
            name: {{name}}
            description: {{desc}}
            ---
            # {{name}}
            """);
        }
        var index = new SkillIndex();
        foreach (var (name, _, source, adapter) in skills)
        {
            var subDir = Path.Combine(dir, source.ToString());
            index.AddRoot(subDir, source, adapter);
        }
        index.Rebuild();
        return index;
    }

    private static void Cleanup(SkillIndex index)
    {
        foreach (var (root, _, _) in index.GetRoots())
        {
            var parent = Directory.GetParent(root);
            if (parent is not null)
            {
                TestDirectory.Delete(parent.FullName);
            }
        }
    }

    [Fact]
    public void Scan_FindsSkillSigil()
    {
        var index = MakeIndex(("git-helper", "git stuff", SkillSource.CoveUser, null));
        try
        {
            var resolver = new SigilResolver(index);
            var matches = resolver.Scan("please help +git-helper with this");

            Assert.Single(matches);
            Assert.Equal("git-helper", matches[0].Name);
            Assert.NotNull(matches[0].Skill);
            Assert.Null(matches[0].Error);
        }
        finally { Cleanup(index); }
    }

    [Fact]
    public void Scan_UnresolvedSigil_RecordsError()
    {
        var index = MakeIndex();
        try
        {
            var resolver = new SigilResolver(index);
            var matches = resolver.Scan("use +nonexistent-skill");

            Assert.Single(matches);
            Assert.Equal("nonexistent-skill", matches[0].Name);
            Assert.Null(matches[0].Skill);
            Assert.Equal("unresolved: +nonexistent-skill", matches[0].Error);
        }
        finally { Cleanup(index); }
    }

    [Fact]
    public void Scan_IgnoresSigilsInCodeFences()
    {
        var index = MakeIndex(("findable", "found", SkillSource.CoveUser, null));
        try
        {
            var resolver = new SigilResolver(index);
            var matches = resolver.Scan("text +findable\n```\n+findable in fence\n```\nmore +findable");

            Assert.Equal(2, matches.Count);
            Assert.All(matches, m => Assert.Equal("findable", m.Name));
        }
        finally { Cleanup(index); }
    }

    [Fact]
    public void Scan_IgnoresInlineCode()
    {
        var index = MakeIndex(("inline", "test", SkillSource.CoveUser, null));
        try
        {
            var resolver = new SigilResolver(index);
            var matches = resolver.Scan("use `+inline` here");

            Assert.Empty(matches);
        }
        finally { Cleanup(index); }
    }

    [Fact]
    public void Scan_MultipleSigilsStack()
    {
        var index = MakeIndex(("alpha", "a", SkillSource.CoveUser, null), ("beta", "b", SkillSource.CoveUser, null));
        try
        {
            var resolver = new SigilResolver(index);
            var matches = resolver.Scan("+alpha and +beta");

            Assert.Equal(2, matches.Count);
            Assert.Equal("alpha", matches[0].Name);
            Assert.Equal("beta", matches[1].Name);
        }
        finally { Cleanup(index); }
    }

    [Fact]
    public void Scan_ScopedBypass_CaseInsensitive()
    {
        var index = MakeIndex(("shared", "cove version", SkillSource.CoveUser, null));
        try
        {
            var resolver = new SigilResolver(index);
            var matches = resolver.Scan("+SHARED@COVE-USER");

            Assert.Single(matches);
            Assert.NotNull(matches[0].Skill);
            Assert.Equal(SkillSource.CoveUser, matches[0].Skill!.Source);
        }
        finally { Cleanup(index); }
    }

    [Fact]
    public void Scan_ScopedBypass_FindsShadowedHarnessSkill()
    {
        var index = MakeIndex(
            ("shared", "cove version", SkillSource.CoveUser, null),
            ("shared", "harness version", SkillSource.Harness, "claude"));
        try
        {
            var resolver = new SigilResolver(index);
            var coveMatch = resolver.Scan("+shared@cove-user");
            var harnessMatch = resolver.Scan("+shared@harness-claude");

            Assert.Single(coveMatch);
            Assert.Equal("cove version", coveMatch[0].Skill!.Description);
            Assert.Single(harnessMatch);
            Assert.Equal("harness version", harnessMatch[0].Skill!.Description);
        }
        finally { Cleanup(index); }
    }

    [Fact]
    public void Scan_ScopedBypass_ResolvesCorrectScope()
    {
        var index = MakeIndex(("shared", "cove version", SkillSource.CoveUser, null));
        try
        {
            var resolver = new SigilResolver(index);
            var matches = resolver.Scan("+shared@cove-user");

            Assert.Single(matches);
            Assert.NotNull(matches[0].Skill);
            Assert.Equal(SkillSource.CoveUser, matches[0].Skill!.Source);
        }
        finally { Cleanup(index); }
    }

    [Fact]
    public void Scan_ScopedBypass_WrongScope_Unresolved()
    {
        var index = MakeIndex(("shared", "cove version", SkillSource.CoveUser, null));
        try
        {
            var resolver = new SigilResolver(index);
            var matches = resolver.Scan("+shared@cove-project");

            Assert.Single(matches);
            Assert.Null(matches[0].Skill);
            Assert.NotNull(matches[0].Error);
        }
        finally { Cleanup(index); }
    }

    [Fact]
    public void Scan_EmptyText_ReturnsEmpty()
    {
        var index = MakeIndex();
        try
        {
            var resolver = new SigilResolver(index);
            Assert.Empty(resolver.Scan(""));
        }
        finally { Cleanup(index); }
    }
}
