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

    [ExternalTheory(TestOperatingSystem.Unix, "bash", "sqlite3")]
    [InlineData("build_launch_command.sh", null)]
    [InlineData("build_resume_command.sh", "thread-1")]
    public async Task BuildCommand_ForwardsExactCoveEnvironmentWithoutDuplicates(string script, string? sessionId)
    {
        var env = CoveEnvironment();
        var result = await RunBuildCommandAsync(script, sessionId, env);

        Assert.True(result.ExitCode == 0, $"exit={result.ExitCode} stderr='{result.Stderr}' stdout='{result.Stdout}'");
        var command = CommandArguments(result.Stdout);
        foreach (var pair in env.Where(pair => pair.Key.StartsWith("COVE", StringComparison.Ordinal)))
        {
            var expected = $"shell_environment_policy.set.{pair.Key}=";
            var matches = command.Where(argument => argument!.StartsWith(expected, StringComparison.Ordinal)).ToArray();
            Assert.Single(matches);
            Assert.Equal(pair.Value, ParseTomlBasicString(matches[0]![expected.Length..]));
        }
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task BuildLaunchCommand_OmitsUnsetOptionalTaskEnvironment()
    {
        var env = CoveEnvironment();
        env["COVE_TASK_ID"] = string.Empty;
        env["COVE_TASK_RUN_ID"] = string.Empty;
        var result = await RunBuildCommandAsync("build_launch_command.sh", null, env);

        Assert.Equal(0, result.ExitCode);
        var command = CommandArguments(result.Stdout);
        Assert.DoesNotContain(command, argument => argument!.Contains("COVE_TASK_ID", StringComparison.Ordinal));
        Assert.DoesNotContain(command, argument => argument!.Contains("COVE_TASK_RUN_ID", StringComparison.Ordinal));
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task BuildLaunchCommand_RejectsMissingManagedIdentityWithoutExposingToken()
    {
        var env = CoveEnvironment();
        env["COVE_NOOK_ID"] = string.Empty;
        var result = await RunBuildCommandAsync("build_launch_command.sh", null, env);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("COVE_NOOK_ID", result.Stderr);
        Assert.DoesNotContain(env["COVE_NOOK_TOKEN"], result.Stderr);
        Assert.DoesNotContain(env["COVE_NOOK_TOKEN"], result.Stdout);
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task BuildLaunchCommand_RoundTripsTomlSpecialCharacters()
    {
        var env = CoveEnvironment();
        env["COVE_DATA_DIR"] = "C:\\Cove “δ”\tline\nnext";
        var result = await RunBuildCommandAsync("build_launch_command.sh", null, env);

        Assert.Equal(0, result.ExitCode);
        var prefix = "shell_environment_policy.set.COVE_DATA_DIR=";
        var argument = Assert.Single(CommandArguments(result.Stdout), value => value!.StartsWith(prefix, StringComparison.Ordinal));
        Assert.Equal(env["COVE_DATA_DIR"], ParseTomlBasicString(argument![prefix.Length..]));
    }

    [ExternalFact(TestOperatingSystem.Unix, "bash")]
    public async Task BuildLaunchCommand_FakeCodexCorePolicyKeepsOnlyExplicitCoveValues()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-codex-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var fakeCodex = Path.Combine(root, "fake-codex");
            File.WriteAllText(fakeCodex, "#!/usr/bin/env bash\nset -euo pipefail\nwhile [ $# -gt 0 ]; do\n  if [ \"$1\" = --config ]; then\n    setting=\"$2\"\n    key=\"${setting%%=*}\"\n    key=\"${key##*.}\"\n    literal=\"${setting#*=}\"\n    eval \"export $key=$literal\"\n    shift 2\n  else\n    shift\n  fi\ndone\nunset UNRELATED_SECRET\nprintf '%s\\n' \"${COVE_CHANNEL:-missing}\" \"${COVE_NOOK_ID:-missing}\" \"${UNRELATED_SECRET:-missing}\"\n");
            using (var chmod = Process.Start("/bin/chmod", new[] { "+x", fakeCodex }))
            {
                Assert.NotNull(chmod);
                await chmod.WaitForExitAsync();
                Assert.Equal(0, chmod.ExitCode);
            }
            var env = CoveEnvironment();
            var built = await RunBuildCommandAsync("build_launch_command.sh", null, env);
            var command = CommandArguments(built.Stdout);
            var start = new ProcessStartInfo(fakeCodex) { RedirectStandardOutput = true, UseShellExecute = false };
            start.Environment.Clear();
            start.Environment["UNRELATED_SECRET"] = "must-not-survive";
            for (var index = 1; index < command.Length; index++) start.ArgumentList.Add(command[index]!);
            using var process = Process.Start(start);
            Assert.NotNull(process);
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
            Assert.Equal([env["COVE_CHANNEL"], env["COVE_NOOK_ID"], "missing"], output.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }
        finally { TestDirectory.Delete(root); }
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
    public async Task HooksInstaller_ReconcilesEverySupportedHookAndPreservesUserHooks()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-codex-hooks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var hooksPath = Path.Combine(root, "hooks.json");
            File.WriteAllText(hooksPath, """
                {"hooks":{"SessionStart":[{"matcher":"startup","hooks":[{"type":"command","command":"user-start"}]}],"PreToolUse":[{"matcher":"Read","hooks":[{"type":"command","command":"user-tool"}]}],"Stop":[{"hooks":[{"type":"command","command":"user-stop"}]}]}}
                """);
            var runner = new MethodRunner();
            var env = new Dictionary<string, string> { ["CODEX_HOME"] = root };
            var install = await runner.RunAsync(AdapterDir, "hooks.sh", new[] { "install" }, TimeSpan.FromSeconds(20), env);
            Assert.True(install.ExitCode == 0, $"exit={install.ExitCode} stderr='{install.Stderr}' stdout='{install.Stdout}'");
            var firstInstall = File.ReadAllText(hooksPath);
            var reinstall = await runner.RunAsync(AdapterDir, "hooks.sh", new[] { "install" }, TimeSpan.FromSeconds(20), env);
            Assert.True(reinstall.ExitCode == 0, $"exit={reinstall.ExitCode} stderr='{reinstall.Stderr}' stdout='{reinstall.Stdout}'");
            Assert.Equal(firstInstall, File.ReadAllText(hooksPath));

            using (var installed = JsonDocument.Parse(File.ReadAllText(hooksPath)))
            {
                var hooks = installed.RootElement.GetProperty("hooks");
                foreach (var eventName in SupportedHookEvents)
                    Assert.Equal(1, CountCoveCommands(hooks.GetProperty(eventName)));
                Assert.Equal("user-start", hooks.GetProperty("SessionStart")[0].GetProperty("hooks")[0].GetProperty("command").GetString());
                Assert.Equal("user-tool", hooks.GetProperty("PreToolUse")[0].GetProperty("hooks")[0].GetProperty("command").GetString());
                Assert.Equal("user-stop", hooks.GetProperty("Stop")[0].GetProperty("hooks")[0].GetProperty("command").GetString());
            }

            var uninstall = await runner.RunAsync(AdapterDir, "hooks.sh", new[] { "uninstall" }, TimeSpan.FromSeconds(20), env);
            Assert.True(uninstall.ExitCode == 0, $"exit={uninstall.ExitCode} stderr='{uninstall.Stderr}' stdout='{uninstall.Stdout}'");
            using var removed = JsonDocument.Parse(File.ReadAllText(hooksPath));
            var removedHooks = removed.RootElement.GetProperty("hooks");
            foreach (var eventName in SupportedHookEvents)
                Assert.Equal(0, CountCoveCommands(removedHooks.GetProperty(eventName)));
            Assert.Equal("user-start", removedHooks.GetProperty("SessionStart")[0].GetProperty("hooks")[0].GetProperty("command").GetString());
            Assert.Equal("user-tool", removedHooks.GetProperty("PreToolUse")[0].GetProperty("hooks")[0].GetProperty("command").GetString());
            Assert.Equal("user-stop", removedHooks.GetProperty("Stop")[0].GetProperty("hooks")[0].GetProperty("command").GetString());
        }
        finally { TestDirectory.Delete(root); }
    }

    [ExternalTheory(TestOperatingSystem.Unix, "bash", "jq")]
    [InlineData("SessionStart", "session-start")]
    [InlineData("UserPromptSubmit", "user-prompt-submit")]
    [InlineData("PreToolUse", "pre-tool-use")]
    [InlineData("PostToolUse", "post-tool-use")]
    [InlineData("PermissionRequest", "permission-request")]
    [InlineData("SubagentStart", "subagent-start")]
    [InlineData("SubagentStop", "subagent-stop")]
    [InlineData("Stop", "stop")]
    public async Task HookCommand_ForwardsOfficialEventAndOriginalEnvelope(string hookEventName, string coveEvent)
    {
        var payload = JsonSerializer.Serialize(new { session_id = "session-1", hook_event_name = hookEventName, tool_name = "Read", nested = new { value = 7 } });
        var result = await RunHookAsync(payload);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Stderr);
        using var invocation = JsonDocument.Parse(result.Invocation);
        Assert.Equal(coveEvent, invocation.RootElement.GetProperty("arguments")[2].GetString());
        Assert.Equal("codex", invocation.RootElement.GetProperty("arguments")[4].GetString());
        Assert.Equal("nook-1", invocation.RootElement.GetProperty("arguments")[6].GetString());
        using var expected = JsonDocument.Parse(payload);
        Assert.True(JsonElement.DeepEquals(expected.RootElement, invocation.RootElement.GetProperty("stdin")));
    }

    [ExternalTheory(TestOperatingSystem.Unix, "bash", "jq")]
    [InlineData("{\"session_id\":\"session-1\"}", "no hook_event_name")]
    [InlineData("{\"session_id\":\"session-1\",\"hook_event_name\":\"SessionEnd\"}", "unknown hook_event_name")]
    [InlineData("not-json", "invalid JSON")]
    public async Task HookCommand_RejectsInvalidOrUnsupportedPayloadNonFatally(string payload, string diagnostic)
    {
        var result = await RunHookAsync(payload);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(diagnostic, result.Stderr);
        Assert.Empty(result.Invocation);
    }

    [ExternalTheory(TestOperatingSystem.Unix, "bash", "sqlite3", "jq")]
    [InlineData("build_launch_command.sh", null)]
    [InlineData("build_resume_command.sh", "thread-1")]
    public async Task BuildCommand_ReconcilesStaleHooksWithoutChangingFlags(string script, string? sessionId)
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-codex-reconcile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "hooks.json"), "{\"hooks\":{\"SessionStart\":[]}}");
            if (sessionId is not null)
            {
                var sqlite = TestPrerequisite.FindExecutable("sqlite3");
                Assert.NotNull(sqlite);
                using var process = Process.Start(new ProcessStartInfo(sqlite) { UseShellExecute = false, ArgumentList = { Path.Combine(root, "state_5.sqlite"), $"CREATE TABLE threads (id TEXT); INSERT INTO threads VALUES ('{sessionId}');" } });
                Assert.NotNull(process);
                await process.WaitForExitAsync();
                Assert.Equal(0, process.ExitCode);
            }
            const string flags = "{\"dangerouslySkipPermissions\":true,\"model\":\"model-x\",\"effort\":\"high\"}";
            var args = sessionId is null ? new[] { flags } : new[] { sessionId, flags };
            var result = await new MethodRunner().RunAsync(AdapterDir, script, args, TimeSpan.FromSeconds(20), new Dictionary<string, string> { ["CODEX_HOME"] = root });

            Assert.True(result.ExitCode == 0, $"exit={result.ExitCode} stderr='{result.Stderr}' stdout='{result.Stdout}'");
            using var commandDocument = JsonDocument.Parse(result.Stdout);
            var command = commandDocument.RootElement.GetProperty("command").EnumerateArray().Select(value => value.GetString()).ToArray();
            Assert.Contains("--yolo", command);
            Assert.Contains("model-x", command);
            Assert.Contains("model_reasoning_effort=\"high\"", command);
            using var hooksDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "hooks.json")));
            foreach (var eventName in SupportedHookEvents)
                Assert.Equal(1, CountCoveCommands(hooksDocument.RootElement.GetProperty("hooks").GetProperty(eventName)));
        }
        finally { TestDirectory.Delete(root); }
    }

    private static readonly string[] SupportedHookEvents = ["SessionStart", "UserPromptSubmit", "PreToolUse", "PostToolUse", "PermissionRequest", "SubagentStart", "SubagentStop", "Stop"];

    private static Dictionary<string, string> CoveEnvironment() => new()
    {
        ["COVE"] = "1",
        ["COVE_CHANNEL"] = "dev-channel",
        ["COVE_CLI_PATH"] = "/tmp/Cove CLI/cove",
        ["COVE_DATA_DIR"] = "/tmp/Cove Data",
        ["COVE_NOOK_ID"] = "nook-test",
        ["COVE_NOOK_TOKEN"] = "token-private-value",
        ["COVE_BAY_ID"] = "bay-test",
        ["COVE_SHORE_ID"] = "shore-test",
        ["COVE_HOOK_PORT"] = "43123",
        ["COVE_SKILL_PATH"] = "/tmp/Cove Skill",
        ["COVE_ADAPTER_DIR"] = AdapterDir,
        ["COVE_TASK_ID"] = "task-test",
        ["COVE_TASK_RUN_ID"] = "run-test",
    };

    private static async Task<MethodResult> RunBuildCommandAsync(string script, string? sessionId, Dictionary<string, string> env)
    {
        string? root = null;
        if (sessionId is not null)
        {
            var sqlite = TestPrerequisite.FindExecutable("sqlite3");
            Assert.NotNull(sqlite);
            root = Path.Combine(Path.GetTempPath(), "cove-codex-env-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            using var sqliteProcess = Process.Start(new ProcessStartInfo(sqlite) { UseShellExecute = false, ArgumentList = { Path.Combine(root, "state_5.sqlite"), $"CREATE TABLE threads (id TEXT); INSERT INTO threads VALUES ('{sessionId}');" } });
            Assert.NotNull(sqliteProcess);
            await sqliteProcess.WaitForExitAsync();
            Assert.Equal(0, sqliteProcess.ExitCode);
            env["CODEX_HOME"] = root;
        }
        try
        {
            return await new MethodRunner().RunAsync(AdapterDir, script, sessionId is null ? [] : [sessionId], TimeSpan.FromSeconds(20), env);
        }
        finally
        {
            if (root is not null) TestDirectory.Delete(root);
        }
    }

    private static string?[] CommandArguments(string stdout)
    {
        using var document = JsonDocument.Parse(stdout);
        return document.RootElement.GetProperty("command").EnumerateArray().Select(value => value.GetString()).ToArray();
    }

    private static string ParseTomlBasicString(string literal)
    {
        Assert.StartsWith("\"", literal);
        Assert.EndsWith("\"", literal);
        return JsonSerializer.Deserialize<string>(literal)!;
    }

    private static int CountCoveCommands(JsonElement groups) => groups.EnumerateArray().Sum(group => group.GetProperty("hooks").EnumerateArray().Count(hook => hook.GetProperty("command").GetString()!.Contains("COVE_HOOK_MARKER=cove-runtime-hook", StringComparison.Ordinal)));

    private static async Task<(int ExitCode, string Stderr, string Invocation)> RunHookAsync(string payload)
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-codex-bridge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var fakeCli = Path.Combine(root, "cove");
            var invocationPath = Path.Combine(root, "invocation.json");
            File.WriteAllText(fakeCli, "#!/usr/bin/env bash\njq -n --argjson arguments \"$(printf '%s\\n' \"$@\" | jq -R . | jq -s .)\" --arg stdin \"$(cat)\" '{arguments:$arguments,stdin:($stdin|fromjson)}' > \"$INVOCATION_PATH\"\n");
            using (var chmod = Process.Start("/bin/chmod", new[] { "+x", fakeCli }))
            {
                Assert.NotNull(chmod);
                await chmod.WaitForExitAsync();
                Assert.Equal(0, chmod.ExitCode);
            }
            var start = new ProcessStartInfo("/bin/bash") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add(Path.Combine(AdapterDir, "cove-hooks.sh"));
            start.Environment["COVE"] = "1";
            start.Environment["COVE_CLI_PATH"] = fakeCli;
            start.Environment["COVE_NOOK_ID"] = "nook-1";
            start.Environment["INVOCATION_PATH"] = invocationPath;
            using var process = Process.Start(start)!;
            await process.StandardInput.WriteAsync(payload);
            process.StandardInput.Close();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.Empty(stdout);
            return (process.ExitCode, stderr, File.Exists(invocationPath) ? File.ReadAllText(invocationPath) : string.Empty);
        }
        finally { TestDirectory.Delete(root); }
    }
}
