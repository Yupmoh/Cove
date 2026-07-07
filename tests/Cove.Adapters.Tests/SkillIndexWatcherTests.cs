using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class SkillIndexWatcherTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-skillwatch-" + Guid.NewGuid().ToString("N"));

    private static void WriteSkill(string dir, string name, string description = "test skill")
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

    private static async Task<bool> WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(50);
        }
        return condition();
    }

    [Fact]
    public async Task Watcher_RebuildsOnNewSkill()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var index = new SkillIndex();
            index.AddRoot(dir, SkillSource.CoveUser);
            index.Rebuild();
            using var watcher = new SkillIndexWatcher(index);
            watcher.Start();

            WriteSkill(dir, "added-skill", "dynamically added");

            var ok = await WaitForAsync(() => index.List().Count == 1, TimeSpan.FromSeconds(5));
            Assert.True(ok, "watcher did not pick up new skill within 5s");
            Assert.Equal("added-skill", index.List()[0].Name);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Watcher_RebuildsOnSkillEdit()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        WriteSkill(dir, "edit-skill", "original");
        try
        {
            var index = new SkillIndex();
            index.AddRoot(dir, SkillSource.CoveUser);
            index.Rebuild();
            using var watcher = new SkillIndexWatcher(index);
            watcher.Start();

            WriteSkill(dir, "edit-skill", "updated description");

            var ok = await WaitForAsync(() => index.List()[0].Description == "updated description", TimeSpan.FromSeconds(5));
            Assert.True(ok, "watcher did not pick up skill edit within 5s");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Watcher_RebuildsOnSkillDelete()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        WriteSkill(dir, "doomed-skill");
        try
        {
            var index = new SkillIndex();
            index.AddRoot(dir, SkillSource.CoveUser);
            index.Rebuild();
            using var watcher = new SkillIndexWatcher(index);
            watcher.Start();

            Directory.Delete(Path.Combine(dir, "doomed-skill"), true);

            var ok = await WaitForAsync(() => index.List().Count == 0, TimeSpan.FromSeconds(5));
            Assert.True(ok, "watcher did not pick up skill deletion within 5s");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
