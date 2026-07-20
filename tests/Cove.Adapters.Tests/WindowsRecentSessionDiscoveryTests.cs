using Xunit;

namespace Cove.Adapters.Tests;

public sealed class WindowsRecentSessionDiscoveryTests
{
    [Fact]
    public void ListOmp_FindsMatchingJsonlSessionWithoutJq()
    {
        var root = NewDir();
        const string cwd = @"D:\NativeSessionFixture";
        try
        {
            var sessionDir = Path.Combine(root, "sessions", "project");
            Directory.CreateDirectory(sessionDir);
            File.WriteAllText(Path.Combine(sessionDir, "session.jsonl"),
                "{\"type\":\"title\",\"title\":\"Recovered OMP\"}\n" +
                "{\"type\":\"session\",\"id\":\"omp-1\",\"cwd\":\"D:\\\\NativeSessionFixture\"}\n");

            var sessions = WindowsRecentSessionDiscovery.ListOmp(cwd, root);

            var session = Assert.Single(sessions);
            Assert.Equal("omp-1", session.Id);
            Assert.Equal("Recovered OMP", session.Name);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ListClaude_FindsMatchingProjectHistoryWithoutJq()
    {
        var root = NewDir();
        const string cwd = @"D:\NativeSessionFixture";
        try
        {
            var projectDir = Path.Combine(root, "projects", "D--NativeSessionFixture");
            Directory.CreateDirectory(projectDir);
            File.WriteAllText(Path.Combine(projectDir, "claude-1.jsonl"),
                "{\"type\":\"user\",\"cwd\":\"D:\\\\NativeSessionFixture\",\"aiTitle\":\"Recovered Claude\"}\n");

            var sessions = WindowsRecentSessionDiscovery.ListClaude(cwd, root);

            var session = Assert.Single(sessions);
            Assert.Equal("claude-1", session.Id);
            Assert.Equal("Recovered Claude", session.Name);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string NewDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "cove-native-sessions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
