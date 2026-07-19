using Cove.Gui;
using Cove.Gui.Tests;
using Cove.Testing;
using Xunit;

public class PerfWriterTests
{
    [Fact]
    public void Save_Writes_Latest_And_Timestamped_Files()
    {
        using var directory = GuiTestDirectory.Create("cove-perf-");
        var dir = Path.Combine(directory.Path, "perf");
        var ret = PerfWriter.Save(dir, "{\"done\":true}", "| a |\n|---|");

        Assert.Equal(dir, ret);
        Assert.True(File.Exists(Path.Combine(dir, "latest.json")));
        Assert.True(File.Exists(Path.Combine(dir, "latest.md")));
        Assert.Equal("{\"done\":true}", File.ReadAllText(Path.Combine(dir, "latest.json")));
        Assert.Single(Directory.GetFiles(dir, "perf-*.json"));
    }

    [Fact]
    public async Task PerfDir_Honors_CoveDataDir_Override()
    {
        await using (await ProcessEnvironmentScope.SetAsync(
            "COVE_DATA_DIR",
            "/tmp/cove-perf-test-root"))
        {
            Assert.Equal(Path.Combine("/tmp/cove-perf-test-root", "cache", "perf"), PerfWriter.PerfDir());
        }
    }
}
