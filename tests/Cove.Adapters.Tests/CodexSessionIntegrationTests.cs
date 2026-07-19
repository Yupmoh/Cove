using System.Diagnostics;
using System.Text.Json;
using Cove.Testing;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class CodexSessionIntegrationTests
{
    private static string AdapterDir => Path.Combine(
        Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
        "adapters", "codex");

    [ExternalFact(TestOperatingSystem.Unix, "bash", "sqlite3")]
    public async Task ListRecent_ReturnsNamedThreadsForNormalizedWorkingDirectory()
    {
        var sqlite = TestPrerequisite.FindExecutable("sqlite3");
        Assert.NotNull(sqlite);

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
        finally { TestDirectory.Delete(root); }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash", "sqlite3")]
    public async Task ListRecent_MergesHistoricalThreadsAcrossCodexStateDatabases()
    {
        var sqlite = TestPrerequisite.FindExecutable("sqlite3");
        Assert.NotNull(sqlite);

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
        finally { TestDirectory.Delete(root); }
    }

    [ExternalTheory(TestOperatingSystem.Unix, "bash", "sqlite3")]
    [InlineData("build_launch_command.sh", null)]
    [InlineData("build_resume_command.sh", "thread-1")]
    public async Task BuildCommand_BypassesTrustForInstalledCoveHook(string script, string? sessionId)
    {
        var runner = new MethodRunner();
        var args = sessionId is null ? Array.Empty<string>() : new[] { sessionId };
        var env = new Dictionary<string, string>();
        string? root = null;
        if (sessionId is not null)
        {
            var sqlite = TestPrerequisite.FindExecutable("sqlite3");
            Assert.NotNull(sqlite);
            root = Path.Combine(Path.GetTempPath(), "cove-codex-resume-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            using var process = Process.Start(new ProcessStartInfo(sqlite)
            {
                UseShellExecute = false,
                ArgumentList = { Path.Combine(root, "state_5.sqlite"), $"CREATE TABLE threads (id TEXT); INSERT INTO threads VALUES ('{sessionId}');" },
            });
            Assert.NotNull(process);
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
            env["CODEX_HOME"] = root;
        }
        var result = await runner.RunAsync(AdapterDir, script, args, TimeSpan.FromSeconds(20), env);

        Assert.True(result.ExitCode == 0, $"exit={result.ExitCode} stderr='{result.Stderr}' stdout='{result.Stdout}'");
        using var document = JsonDocument.Parse(result.Stdout);
        var command = document.RootElement.GetProperty("command").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Contains("--dangerously-bypass-hook-trust", command);
        if (sessionId is not null)
        {
            Assert.Contains("resume", command);
            Assert.Contains(sessionId, command);
        }
        if (root is not null) TestDirectory.Delete(root);
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task BuildResumeCommand_FallsBackToFreshWhenSessionIsMissingFromCodexState()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-codex-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var result = await new MethodRunner().RunAsync(
                AdapterDir,
                "build_resume_command.sh",
                new[] { "missing-thread" },
                TimeSpan.FromSeconds(20),
                new Dictionary<string, string> { ["CODEX_HOME"] = root });
            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.Stdout);
            var command = document.RootElement.GetProperty("command").EnumerateArray().Select(value => value.GetString()).ToArray();
            Assert.DoesNotContain("resume", command);
            Assert.DoesNotContain("missing-thread", command);
        }
        finally { TestDirectory.Delete(root); }
    }

    [ExternalTheory(TestOperatingSystem.Unix, "bash", "sqlite3")]
    [InlineData("build_launch_command.sh", null)]
    [InlineData("build_resume_command.sh", "thread-1")]
    public async Task BuildCommand_MapsDangerouslySkipPermissionsToYolo(string script, string? sessionId)
    {
        var runner = new MethodRunner();
        var flags = """{"dangerouslySkipPermissions":true}""";
        var args = sessionId is null ? new[] { flags } : new[] { sessionId, flags };
        var env = new Dictionary<string, string>();
        string? root = null;
        if (sessionId is not null)
        {
            var sqlite = TestPrerequisite.FindExecutable("sqlite3");
            Assert.NotNull(sqlite);
            root = Path.Combine(Path.GetTempPath(), "cove-codex-resume-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            using var process = Process.Start(new ProcessStartInfo(sqlite)
            {
                UseShellExecute = false,
                ArgumentList = { Path.Combine(root, "state_5.sqlite"), $"CREATE TABLE threads (id TEXT); INSERT INTO threads VALUES ('{sessionId}');" },
            });
            Assert.NotNull(process);
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
            env["CODEX_HOME"] = root;
        }
        var result = await runner.RunAsync(AdapterDir, script, args, TimeSpan.FromSeconds(20), env);

        Assert.True(result.ExitCode == 0, $"exit={result.ExitCode} stderr='{result.Stderr}' stdout='{result.Stdout}'");
        using var document = JsonDocument.Parse(result.Stdout);
        var command = document.RootElement.GetProperty("command").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Contains("--yolo", command);
        if (root is not null) TestDirectory.Delete(root);
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task HookCommand_ExitsSilentlyOutsideCove()
    {
        var runner = new MethodRunner();
        var env = new Dictionary<string, string> { ["COVE"] = "0" };
        var result = await runner.RunAsync(AdapterDir, "cove-hooks.sh", Array.Empty<string>(), TimeSpan.FromSeconds(20), env);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Stdout);
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task HooksInstaller_AddsAndRemovesCoveSessionStartHook()
    {
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
        finally { TestDirectory.Delete(root); }
    }
}
