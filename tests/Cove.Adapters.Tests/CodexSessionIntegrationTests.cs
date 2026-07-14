using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class CodexSessionIntegrationTests
{
    private static string AdapterDir => Path.Combine(
        Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
        "adapters", "codex");

    private static string? FindExecutable(string name)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    [Fact]
    public async Task ListRecent_ReturnsNamedThreadsForNormalizedWorkingDirectory()
    {
        if (OperatingSystem.IsWindows()) return;
        var sqlite = FindExecutable("sqlite3");
        if (sqlite is null) return;

        var root = Path.Combine(Path.GetTempPath(), "cove-codex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var database = Path.Combine(root, "state_5.sqlite");
            var sql = "CREATE TABLE threads (id TEXT, title TEXT, cwd TEXT, updated_at INTEGER, archived INTEGER);" +
                "INSERT INTO threads VALUES ('thread-1','Named Codex thread','/repo/work',1784016000,0);" +
                "INSERT INTO threads VALUES ('thread-2','Other repo','/repo/other',1784017000,0);" +
                "INSERT INTO threads VALUES ('thread-3','Archived','/repo/work',1784018000,1);";
            using (var process = Process.Start(new ProcessStartInfo(sqlite)
            {
                UseShellExecute = false,
                ArgumentList = { database, sql },
            }))
            {
                Assert.NotNull(process);
                await process.WaitForExitAsync();
                Assert.Equal(0, process.ExitCode);
            }

            var runner = new MethodRunner();
            var env = new Dictionary<string, string> { ["CODEX_HOME"] = root };
            var result = await runner.RunAsync(AdapterDir, "list_recent_sessions.sh", new[] { "/repo/work/" }, TimeSpan.FromSeconds(20), env);

            Assert.True(result.ExitCode == 0, $"exit={result.ExitCode} stderr='{result.Stderr}' stdout='{result.Stdout}'");
            using var document = JsonDocument.Parse(result.Stdout);
            var sessions = document.RootElement.GetProperty("sessions");
            Assert.Equal(1, sessions.GetArrayLength());
            Assert.Equal("thread-1", sessions[0].GetProperty("id").GetString());
            Assert.Equal("Named Codex thread", sessions[0].GetProperty("name").GetString());
            Assert.Equal("/repo/work", sessions[0].GetProperty("cwd").GetString());
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task ListRecent_MergesHistoricalThreadsAcrossCodexStateDatabases()
    {
        if (OperatingSystem.IsWindows()) return;
        var sqlite = FindExecutable("sqlite3");
        if (sqlite is null) return;

        var root = Path.Combine(Path.GetTempPath(), "cove-codex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var database in new[] { "state_4.sqlite", "state_5.sqlite" })
            {
                var id = database == "state_4.sqlite" ? "historical-thread" : "current-thread";
                var sql = "CREATE TABLE threads (id TEXT, title TEXT, cwd TEXT, updated_at INTEGER, archived INTEGER);" +
                    $"INSERT INTO threads VALUES ('{id}','{id}','/repo/work',1784016000,0);";
                using var process = Process.Start(new ProcessStartInfo(sqlite)
                {
                    UseShellExecute = false,
                    ArgumentList = { Path.Combine(root, database), sql },
                });
                Assert.NotNull(process);
                await process.WaitForExitAsync();
                Assert.Equal(0, process.ExitCode);
            }

            var runner = new MethodRunner();
            var env = new Dictionary<string, string> { ["CODEX_HOME"] = root };
            var result = await runner.RunAsync(AdapterDir, "list_recent_sessions.sh", new[] { "/repo/work" }, TimeSpan.FromSeconds(20), env);

            Assert.True(result.ExitCode == 0, $"exit={result.ExitCode} stderr='{result.Stderr}' stdout='{result.Stdout}'");
            using var document = JsonDocument.Parse(result.Stdout);
            var ids = document.RootElement.GetProperty("sessions").EnumerateArray().Select(session => session.GetProperty("id").GetString()).ToArray();
            Assert.Equal(2, ids.Length);
            Assert.Contains("historical-thread", ids);
            Assert.Contains("current-thread", ids);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Theory]
    [InlineData("build_launch_command.sh", null)]
    [InlineData("build_resume_command.sh", "thread-1")]
    public async Task BuildCommand_BypassesTrustForInstalledCoveHook(string script, string? sessionId)
    {
        if (OperatingSystem.IsWindows()) return;

        var runner = new MethodRunner();
        var args = sessionId is null ? Array.Empty<string>() : new[] { sessionId };
        var result = await runner.RunAsync(AdapterDir, script, args, TimeSpan.FromSeconds(20));

        Assert.True(result.ExitCode == 0, $"exit={result.ExitCode} stderr='{result.Stderr}' stdout='{result.Stdout}'");
        using var document = JsonDocument.Parse(result.Stdout);
        var command = document.RootElement.GetProperty("command").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Contains("--dangerously-bypass-hook-trust", command);
        if (sessionId is not null)
        {
            Assert.Contains("resume", command);
            Assert.Contains(sessionId, command);
        }
    }

    [Theory]
    [InlineData("build_launch_command.sh", null)]
    [InlineData("build_resume_command.sh", "thread-1")]
    public async Task BuildCommand_MapsDangerouslySkipPermissionsToYolo(string script, string? sessionId)
    {
        if (OperatingSystem.IsWindows()) return;

        var runner = new MethodRunner();
        var flags = """{"dangerouslySkipPermissions":true}""";
        var args = sessionId is null ? new[] { flags } : new[] { sessionId, flags };
        var result = await runner.RunAsync(AdapterDir, script, args, TimeSpan.FromSeconds(20));

        Assert.True(result.ExitCode == 0, $"exit={result.ExitCode} stderr='{result.Stderr}' stdout='{result.Stdout}'");
        using var document = JsonDocument.Parse(result.Stdout);
        var command = document.RootElement.GetProperty("command").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Contains("--yolo", command);
    }

    [Fact]
    public async Task HooksInstaller_AddsAndRemovesCoveSessionStartHook()
    {
        if (OperatingSystem.IsWindows()) return;

        var root = Path.Combine(Path.GetTempPath(), "cove-codex-hooks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var runner = new MethodRunner();
            var env = new Dictionary<string, string> { ["CODEX_HOME"] = root };
            var install = await runner.RunAsync(AdapterDir, "hooks.sh", new[] { "install" }, TimeSpan.FromSeconds(20), env);
            Assert.True(install.ExitCode == 0, $"exit={install.ExitCode} stderr='{install.Stderr}' stdout='{install.Stdout}'");

            var hooksPath = Path.Combine(root, "hooks.json");
            using (var installed = JsonDocument.Parse(File.ReadAllText(hooksPath)))
            {
                var command = installed.RootElement
                    .GetProperty("hooks")
                    .GetProperty("SessionStart")[0]
                    .GetProperty("hooks")[0]
                    .GetProperty("command")
                    .GetString();
                Assert.Contains("cove-hooks.sh", command);
            }

            var uninstall = await runner.RunAsync(AdapterDir, "hooks.sh", new[] { "uninstall" }, TimeSpan.FromSeconds(20), env);
            Assert.True(uninstall.ExitCode == 0, $"exit={uninstall.ExitCode} stderr='{uninstall.Stderr}' stdout='{uninstall.Stdout}'");
            using var removed = JsonDocument.Parse(File.ReadAllText(hooksPath));
            Assert.Empty(removed.RootElement.GetProperty("hooks").GetProperty("SessionStart").EnumerateArray());
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
