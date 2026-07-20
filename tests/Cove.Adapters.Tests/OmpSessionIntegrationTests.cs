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

    [ExternalFact(TestOperatingSystem.Any, "bun")]
    public async Task CoveHooks_EmitActivityTransitionsForAgentTurns()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-omp-hooks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var moduleUri = new Uri(Path.Combine(AdapterDir, "cove-hooks.ts")).AbsoluteUri;
            var harness = """
                const module = await import(__MODULE__);
                const handlers = new Map();
                const emitted = [];
                const pi = { on: (event, handler) => handlers.set(event, handler) };
                module.registerCoveHooks(pi, async (event, payload) => emitted.push({ event, payload }));
                const context = { sessionManager: { getSessionFile: () => "/tmp/stamp_session-1.jsonl" } };
                const events = [
                  ["session_start", { type: "session_start", reason: "startup" }],
                  ["agent_start", { type: "agent_start" }],
                  ["tool_execution_start", { type: "tool_execution_start", toolCallId: "call-1", toolName: "read", args: {} }],
                  ["tool_execution_end", { type: "tool_execution_end", toolCallId: "call-1", toolName: "read", result: {}, isError: false }],
                  ["tool_execution_start", { type: "tool_execution_start", toolCallId: "call-2", toolName: "ask", args: {} }],
                  ["tool_execution_end", { type: "tool_execution_end", toolCallId: "call-2", toolName: "ask", result: {}, isError: false }],
                  ["agent_end", { type: "agent_end", messages: [] }],
                  ["session_shutdown", { type: "session_shutdown" }],
                ];
                for (const [name, event] of events) {
                  const handler = handlers.get(name);
                  if (!handler) throw new Error(`missing handler ${name}`);
                  await handler(event, context);
                }
                process.stdout.write(JSON.stringify(emitted));
                """.Replace("__MODULE__", JsonSerializer.Serialize(moduleUri), StringComparison.Ordinal);
            var harnessPath = Path.Combine(root, "harness.ts");
            File.WriteAllText(harnessPath, harness);
            var startInfo = new System.Diagnostics.ProcessStartInfo("bun")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add(harnessPath);
            using var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("bun process did not start");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            Assert.True(process.ExitCode == 0, $"exit={process.ExitCode} stderr='{stderr}' stdout='{stdout}'");
            using var document = JsonDocument.Parse(stdout);
            var events = document.RootElement.EnumerateArray()
                .Select(entry => entry.GetProperty("event").GetString() ?? "")
                .ToArray();
            Assert.Equal(
                ["session-start", "user-prompt-submit", "pre-tool-use", "post-tool-use", "notification", "post-tool-use", "stop", "session-end"],
                events);
            Assert.Equal("session-1", document.RootElement[0].GetProperty("payload").GetProperty("session_id").GetString());
            Assert.Equal("read", document.RootElement[2].GetProperty("payload").GetProperty("tool_name").GetString());
            Assert.Equal("ask", document.RootElement[4].GetProperty("payload").GetProperty("tool_name").GetString());
        }
        finally { TestDirectory.Delete(root); }
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
