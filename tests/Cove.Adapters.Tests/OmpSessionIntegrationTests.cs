using System.Text.Json;
using Cove.Testing;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class OmpSessionIntegrationTests
{
    private static string AdapterDir => Path.Combine(
        Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
        "adapters", "omp");

    [ExternalFact(TestOperatingSystem.Unix, "bash", "jq")]
    public async Task ListRecent_ReturnsNamedSessionsForRequestedWorkingDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-omp-" + Guid.NewGuid().ToString("N"));
        var cwd = "/repo/work/";
        try
        {
            var sessionsDir = Path.Combine(root, "sessions", "-repo-work");
            Directory.CreateDirectory(sessionsDir);
            File.WriteAllText(
                Path.Combine(sessionsDir, "2026-07-14T06-25-14-013Z_session-1.jsonl"),
                "{\"type\":\"title\",\"title\":\"Initial title\"}\n" +
                "{\"type\":\"session\",\"id\":\"session-1\",\"cwd\":\"/repo/work\"}\n" +
                "{\"type\":\"title_change\",\"title\":\"Named session\"}\n");
            File.WriteAllText(
                Path.Combine(sessionsDir, "2026-07-14T06-25-14-013Z_other.jsonl"),
                "{\"type\":\"session\",\"id\":\"other\",\"cwd\":\"/repo/other\"}\n");

            var runner = new MethodRunner();
            var env = new Dictionary<string, string> { ["PI_CODING_AGENT_DIR"] = root };
            var result = await runner.RunAsync(AdapterDir, "list_recent_sessions.sh", new[] { cwd }, TimeSpan.FromSeconds(20), env);

            Assert.True(result.ExitCode == 0, $"exit={result.ExitCode} stderr='{result.Stderr}' stdout='{result.Stdout}'");
            using var document = JsonDocument.Parse(result.Stdout);
            var sessions = document.RootElement.GetProperty("sessions");
            Assert.Equal(1, sessions.GetArrayLength());
            Assert.Equal("session-1", sessions[0].GetProperty("id").GetString());
            Assert.Equal("Named session", sessions[0].GetProperty("name").GetString());
            Assert.Equal("/repo/work", sessions[0].GetProperty("cwd").GetString());
        }
        finally { TestDirectory.Delete(root); }
    }

    [ExternalTheory(TestOperatingSystem.Unix, "bash")]
    [InlineData("build_launch_command.sh", null)]
    [InlineData("build_resume_command.sh", "session-1")]
    public async Task BuildCommand_LoadsCoveHookExtension(string script, string? sessionId)
    {
        var runner = new MethodRunner();
        var args = sessionId is null ? Array.Empty<string>() : new[] { sessionId };
        var env = new Dictionary<string, string>();
        string? root = null;
        if (sessionId is not null)
        {
            root = Path.Combine(Path.GetTempPath(), "cove-omp-resume-" + Guid.NewGuid().ToString("N"));
            var sessionDir = Path.Combine(root, "sessions", "repo");
            Directory.CreateDirectory(sessionDir);
            File.WriteAllText(Path.Combine(sessionDir, $"stamp_{sessionId}.jsonl"), "{}\n");
            env["PI_CODING_AGENT_DIR"] = root;
        }
        var result = await runner.RunAsync(AdapterDir, script, args, TimeSpan.FromSeconds(20), env);

        Assert.True(result.ExitCode == 0, $"exit={result.ExitCode} stderr='{result.Stderr}' stdout='{result.Stdout}'");
        using var document = JsonDocument.Parse(result.Stdout);
        var command = document.RootElement.GetProperty("command").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Contains("--hook", command);
        Assert.Contains("--allow-home", command);
        Assert.Contains(command, value => value is not null && value.EndsWith("cove-hooks.ts", StringComparison.Ordinal));
        if (sessionId is not null)
        {
            Assert.Contains("--resume", command);
            Assert.Contains(sessionId, command);
        }
        if (root is not null) TestDirectory.Delete(root);
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task BuildResumeCommand_FallsBackToFreshWhenSessionIsMissingFromOmpState()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-omp-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var result = await new MethodRunner().RunAsync(
                AdapterDir,
                "build_resume_command.sh",
                new[] { "missing-session" },
                TimeSpan.FromSeconds(20),
                new Dictionary<string, string> { ["PI_CODING_AGENT_DIR"] = root });
            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.Stdout);
            var command = document.RootElement.GetProperty("command").EnumerateArray().Select(value => value.GetString()).ToArray();
            Assert.DoesNotContain("--resume", command);
            Assert.DoesNotContain("missing-session", command);
        }
        finally { TestDirectory.Delete(root); }
    }
}
