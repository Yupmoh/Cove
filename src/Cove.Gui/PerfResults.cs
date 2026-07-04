using Ryn.Ipc;

namespace Cove.Gui;

public static class PerfWriter
{
    public static string Save(string baseDir, string json, string markdown)
    {
        Directory.CreateDirectory(baseDir);
        var ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        File.WriteAllText(Path.Combine(baseDir, $"perf-{ts}.json"), json);
        File.WriteAllText(Path.Combine(baseDir, "latest.json"), json);
        File.WriteAllText(Path.Combine(baseDir, "latest.md"), markdown);
        return baseDir;
    }

    public static string PerfDir()
    {
        var overrideRoot = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        var root = !string.IsNullOrEmpty(overrideRoot)
            ? overrideRoot
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cove-dev");
        return Path.Combine(root, "cache", "perf");
    }
}

public static class PerfResultsCommand
{
    [RynCommand("app.savePerf")]
    public static string SavePerf(string json, string markdown) => PerfWriter.Save(PerfWriter.PerfDir(), json, markdown);
}
