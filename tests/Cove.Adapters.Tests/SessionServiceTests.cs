using System.Text.Json;
using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class SessionServiceTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-sess-" + Guid.NewGuid().ToString("N"));

    private static string WriteScript(string dir, string name, string content)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "#!/usr/bin/env bash\nset -euo pipefail\n" + content);
        if (!OperatingSystem.IsWindows())
            System.IO.File.SetUnixFileMode(path, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);
        return path;
    }

    [Fact]
    public async Task ListRecentSessions_ParsesJsonAndSortsByLastActiveDesc()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        try
        {
            WriteScript(dir, "list_recent_sessions.sh", """
            echo '{"sessions":[
              {"id":"s1","name":"old","cwd":"/repo","lastActive":"2024-01-01T00:00:00Z"},
              {"id":"s2","name":"new","cwd":"/repo","lastActive":"2024-06-01T00:00:00Z"},
              {"id":"s3","name":"mid","cwd":"/repo","lastActive":"2024-03-01T00:00:00Z"}
            ]}'
            """);
            var runner = new MethodRunner();
            var svc = new SessionService(runner);
            var sessions = await svc.ListRecentSessionsAsync(dir, "/repo");

            if (sessions.Count != 3)
            {
                var raw = await runner.RunAsync(dir, "list_recent_sessions.sh", ["/repo"], TimeSpan.FromSeconds(10));
                Assert.Fail($"expected 3 sessions, got {sessions.Count}; direct rerun exit={raw.ExitCode} stdout='{raw.Stdout}' stderr='{raw.Stderr}'");
            }
            Assert.Equal(3, sessions.Count);
            Assert.Equal("s2", sessions[0].Id);
            Assert.Equal("s3", sessions[1].Id);
            Assert.Equal("s1", sessions[2].Id);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ListRecentSessions_CachesPerAdapterCwd_WithinTtl()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        try
        {
            var marker = Path.Combine(dir, "calls");
            WriteScript(dir, "list_recent_sessions.sh", $$"""
            echo x >> "{{marker}}"
            echo '{"sessions":[{"id":"s1","cwd":"/repo","lastActive":"2024-01-01T00:00:00Z"}]}'
            """);
            var runner = new MethodRunner();
            var svc = new SessionService(runner, cacheTtl: TimeSpan.FromSeconds(5));

            await svc.ListRecentSessionsAsync(dir, "/repo");
            await svc.ListRecentSessionsAsync(dir, "/repo");

            Assert.True(File.Exists(marker));
            Assert.Equal("x\n", await File.ReadAllTextAsync(marker));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
    [Fact]
    public async Task ListRecentSessions_GracefulFailure_ReturnsEmpty()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        try
        {
            WriteScript(dir, "list_recent_sessions.sh", "echo 'not found' >&2; exit 1");
            var runner = new MethodRunner();
            var svc = new SessionService(runner);
            var sessions = await svc.ListRecentSessionsAsync(dir, "/repo");

            Assert.Empty(sessions);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ExtractSession_ParsesCanonicalEventJsonL()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        try
        {
            WriteScript(dir, "extract.sh", """
            echo '{"type":"session_start","sessionId":"s1","cwd":"/repo","timestamp":"2024-01-01T00:00:00Z"}'
            echo '{"type":"prose","role":"user","content":"hello","timestamp":"2024-01-01T00:01:00Z"}'
            echo '{"type":"session_end","sessionId":"s1","timestamp":"2024-01-01T00:05:00Z"}'
            """);
            var runner = new MethodRunner();
            var svc = new SessionService(runner);
            var events = await svc.ExtractSessionAsync(dir, "extract.sh", "s1", "/repo", "quick");

            Assert.Equal(3, events.Count);
            Assert.Equal("session_start", events[0].Type);
            Assert.Equal("prose", events[1].Type);
            Assert.Equal("session_end", events[2].Type);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ExtractSession_SkipsInvalidJsonLines()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        try
        {
            WriteScript(dir, "extract.sh", """
            echo '{"type":"session_start","sessionId":"s1"}'
            echo 'not json'
            echo '{"type":"session_end","sessionId":"s1"}'
            """);
            var runner = new MethodRunner();
            var svc = new SessionService(runner);
            var events = await svc.ExtractSessionAsync(dir, "extract.sh", "s1", "/repo", "standard");

            Assert.Equal(2, events.Count);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ExtractSession_SchemaVersionBump_FlagsReindex()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        try
        {
            WriteScript(dir, "extract.sh", "echo '{\"type\":\"session_start\"}'");
            var runner = new MethodRunner();
            var svc = new SessionService(runner);

            var needsReindex = svc.CheckSchemaVersion("claude-code", oldSchema: 1, newSchema: 2);

            Assert.True(needsReindex);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task ExtractSession_SchemaVersionSame_NoReindex()
    {
        var runner = new MethodRunner();
        var svc = new SessionService(runner);

        var needsReindex = svc.CheckSchemaVersion("claude-code", oldSchema: 2, newSchema: 2);

        Assert.False(needsReindex);
    }
}
