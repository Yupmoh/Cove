using Cove.Adapters;
using Cove.Testing;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class ClaudeCodeRecentSessionNamesTests
{
    private static string AdapterDir => Path.Combine(
        Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
        "adapters", "claude-code");

    [ExternalFact(TestOperatingSystem.Unix, "bash", "jq")]
    public async Task ListRecent_PrefersClaudeSessionName_OverAiTitle()
    {
        var config = Path.Combine(Path.GetTempPath(), "cove-cc-" + Guid.NewGuid().ToString("N"));
        var cwd = "/repo/work";
        var slug = "-repo-work";
        try
        {
            var projectDir = Path.Combine(config, "projects", slug);
            Directory.CreateDirectory(projectDir);
            File.WriteAllText(Path.Combine(projectDir, "s1.jsonl"), "{\"aiTitle\":\"auto title\"}\n");
            File.WriteAllText(Path.Combine(projectDir, "s2.jsonl"), "{\"aiTitle\":\"second auto\"}\n");

            var sessionsDir = Path.Combine(config, "sessions");
            Directory.CreateDirectory(sessionsDir);
            File.WriteAllText(Path.Combine(sessionsDir, "111.json"), "{\"pid\":111,\"sessionId\":\"s1\",\"cwd\":\"/repo/work\",\"name\":\"my named session\",\"status\":\"busy\"}\n");
            File.WriteAllText(Path.Combine(sessionsDir, "222.json"), "{\"pid\":222,\"sessionId\":\"s2\",\"cwd\":\"/repo/work\",\"name\":\"\",\"status\":\"idle\"}\n");

            var runner = new MethodRunner();
            var env = new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = config };
            var result = await runner.RunAsync(AdapterDir, "list_recent_sessions.sh", new[] { cwd }, TimeSpan.FromSeconds(20), env);

            Assert.True(result.ExitCode == 0, $"exit={result.ExitCode} stderr='{result.Stderr}' stdout='{result.Stdout}'");
            Assert.Contains("my named session", result.Stdout);
            Assert.Contains("second auto", result.Stdout);
            Assert.DoesNotContain("auto title", result.Stdout);
        }
        finally { TestDirectory.Delete(config); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash", "jq")]
    public async Task ListRecent_MissingSessionsDir_FallsBackToAiTitle()
    {
        var config = Path.Combine(Path.GetTempPath(), "cove-cc-" + Guid.NewGuid().ToString("N"));
        var cwd = "/repo/work/";
        var slug = "-repo-work";
        try
        {
            var projectDir = Path.Combine(config, "projects", slug);
            Directory.CreateDirectory(projectDir);
            File.WriteAllText(Path.Combine(projectDir, "s1.jsonl"), "{\"aiTitle\":\"auto title\"}\n");

            var runner = new MethodRunner();
            var env = new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = config };
            var result = await runner.RunAsync(AdapterDir, "list_recent_sessions.sh", new[] { cwd }, TimeSpan.FromSeconds(20), env);

            Assert.True(result.ExitCode == 0, $"exit={result.ExitCode} stderr='{result.Stderr}' stdout='{result.Stdout}'");
            Assert.Contains("auto title", result.Stdout);
        }
        finally { TestDirectory.Delete(config); }
    }
}
