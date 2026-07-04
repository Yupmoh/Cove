using Cove.Gui;
using Xunit;

public class PerfWriterTests
{
    [Fact]
    public void Save_Writes_Latest_And_Timestamped_Files()
    {
        var dir = Path.Combine(Directory.CreateTempSubdirectory().FullName, "perf");
        var ret = PerfWriter.Save(dir, "{\"done\":true}", "| a |\n|---|");

        Assert.Equal(dir, ret);
        Assert.True(File.Exists(Path.Combine(dir, "latest.json")));
        Assert.True(File.Exists(Path.Combine(dir, "latest.md")));
        Assert.Equal("{\"done\":true}", File.ReadAllText(Path.Combine(dir, "latest.json")));
        Assert.Single(Directory.GetFiles(dir, "perf-*.json"));
    }

    [Fact]
    public void PerfDir_Honors_CoveDataDir_Override()
    {
        var prev = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("COVE_DATA_DIR", "/tmp/cove-perf-test-root");
            Assert.Equal(Path.Combine("/tmp/cove-perf-test-root", "cache", "perf"), PerfWriter.PerfDir());
        }
        finally { Environment.SetEnvironmentVariable("COVE_DATA_DIR", prev); }
    }
}
