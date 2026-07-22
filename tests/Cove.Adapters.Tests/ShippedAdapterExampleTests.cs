using Cove.Testing;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class ShippedAdapterExampleTests
{
    private static string AdaptersRoot => Path.Combine(
        Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
        "adapters");

    [Theory]
    [InlineData("claude-code", "claude", "#fab387")]
    [InlineData("codex", "codex", "#a6e3a1")]
    [InlineData("omp", "omp", "#cba6f7")]
    public void ShippedAdapter_ParsesAndValidates(string adapterName, string binary, string accent)
    {
        var manifestPath = Path.Combine(AdaptersRoot, adapterName, "adapter.json");
        Assert.True(File.Exists(manifestPath), $"missing manifest: {manifestPath}");

        var json = File.ReadAllText(manifestPath);
        var (manifest, errors) = ManifestValidator.Parse(json);

        Assert.True(manifest is not null, $"{adapterName} errors: {string.Join("; ", errors.Select(e => $"{e.Field}:{e.Code}:{e.Message}"))}");
        Assert.Equal(adapterName, manifest!.Name);
        Assert.Equal(binary, manifest.Binary);
        Assert.Equal(accent, manifest.Accent);
        Assert.Equal(2, manifest.SdkVersion);
        Assert.NotEmpty(manifest.BinaryDiscovery?.Commands ?? []);
        Assert.Contains("build_launch_command", manifest.Methods.Keys);
        Assert.Contains("build_resume_command", manifest.Methods.Keys);
        Assert.Contains("list_recent_sessions", manifest.Methods.Keys);
    }

    [Theory]
    [InlineData("claude-code", "~/.claude/local")]
    [InlineData("codex", null)]
    [InlineData("cursor-agent", null)]
    [InlineData("hermes", null)]
    [InlineData("omp", null)]
    [InlineData("openclaw", null)]
    [InlineData("opencode", null)]
    [InlineData("pi", null)]
    public void ShippedManifestsKeepOnlyAdapterSpecificExecutableRoots(
        string adapterName,
        string? expectedPath)
    {
        var json = File.ReadAllText(Path.Combine(AdaptersRoot, adapterName, "adapter.json"));
        var (manifest, errors) = ManifestValidator.Parse(json);

        Assert.Empty(errors);
        Assert.NotNull(manifest);
        Assert.Equal(expectedPath is null ? [] : [expectedPath], manifest!.BinaryDiscovery!.WellKnownPaths);
        Assert.DoesNotContain(
            manifest.BinaryDiscovery.WellKnownPaths,
            path => path is "/opt/homebrew/bin"
                or "/usr/local/bin"
                or "~/.bun/bin"
                or "~/.npm-global/bin"
                or "~/.local/bin");
    }

    [Fact]
    public void ClaudeHooksSettings_HasNotificationMatchersForPermissionAndIdle()
    {
        var path = Path.Combine(AdaptersRoot, "claude-code", "hooks-settings.json");
        Assert.True(File.Exists(path), $"missing: {path}");

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var hooks = doc.RootElement.GetProperty("hooks");
        Assert.True(hooks.TryGetProperty("Notification", out var notification), "hooks.Notification group missing");

        string? permissionCommand = null;
        string? idleCommand = null;
        foreach (var group in notification.EnumerateArray())
        {
            var matcher = group.GetProperty("matcher").GetString();
            var entry = group.GetProperty("hooks")[0];
            Assert.Equal("command", entry.GetProperty("type").GetString());
            var command = entry.GetProperty("command").GetString();
            if (matcher == "permission_prompt") permissionCommand = command;
            if (matcher == "idle_prompt") idleCommand = command;
        }

        Assert.NotNull(permissionCommand);
        Assert.NotNull(idleCommand);
        Assert.Contains("hook emit permission-request", permissionCommand!);
        Assert.Contains("hook emit notification", idleCommand!);
        Assert.Contains("\"$COVE_CLI_PATH\"", permissionCommand!);
        Assert.Contains("--adapter claude-code", permissionCommand!);
        Assert.Contains("--nook-id \"$COVE_NOOK_ID\"", permissionCommand!);
        Assert.Contains("--nook-id \"$COVE_NOOK_ID\"", idleCommand!);
    }

    [Fact]
    public void CodexManifest_DeclaresEverySupportedLiveHook()
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AdaptersRoot, "codex", "adapter.json")));
        var hooks = document.RootElement.GetProperty("hooks").EnumerateObject().Select(property => property.Name).Order().ToArray();
        var expected = new[] { "permission-request", "post-tool-use", "pre-tool-use", "session-start", "stop", "subagent-start", "subagent-stop", "user-prompt-submit" }.Order().ToArray();
        Assert.Equal(expected, hooks);
        Assert.DoesNotContain("session-end", hooks);
    }

    [Theory]
    [InlineData("claude-code", "~/.claude/skills/cove/SKILL.md")]
    [InlineData("codex", "~/.agents/skills/cove/SKILL.md")]
    [InlineData("cursor-agent", "~/.agents/skills/cove/SKILL.md")]
    [InlineData("hermes", "~/.hermes/skills/cove/SKILL.md")]
    [InlineData("omp", "~/.omp/agent/skills/cove/SKILL.md")]
    [InlineData("openclaw", "~/.agents/skills/cove/SKILL.md")]
    [InlineData("opencode", "~/.agents/skills/cove/SKILL.md")]
    [InlineData("pi", "~/.agents/skills/cove/SKILL.md")]
    public void ShippedCoveSkill_ProvidesDocumentedControlRecipes(
        string adapterName,
        string installPath)
    {
        var adapterDir = Path.Combine(AdaptersRoot, adapterName);
        var manifestJson = File.ReadAllText(
            Path.Combine(adapterDir, "adapter.json"));
        var (manifest, errors) = ManifestValidator.Parse(manifestJson);
        Assert.Empty(errors);
        Assert.NotNull(manifest);
        Assert.Equal(installPath, manifest!.SkillInstallPath);

        var skillPath = Path.Combine(AdaptersRoot, "cove", "skill.md");
        Assert.True(File.Exists(skillPath), $"missing skill: {skillPath}");
        var skill = File.ReadAllText(skillPath);
        Assert.StartsWith("---\nname: cove\n", skill);
        Assert.Contains("$COVE_CLI_PATH", skill);
        Assert.Contains("$COVE_NOOK_ID", skill);
        Assert.Contains("workspace context", skill);
        Assert.Contains("session recent", skill);
        Assert.Contains("agent launch", skill);
        Assert.Contains("agent resume", skill);
        Assert.Contains("nook restart", skill);
        Assert.Contains("nook open terminal", skill);
        Assert.Contains("nook open browser", skill);
        Assert.Contains("--command", skill);
        Assert.Contains("--arg", skill);
        Assert.Contains("--url", skill);
        Assert.Contains("--model", skill);
        Assert.Contains("--effort", skill);
        Assert.Contains("nook close", skill);
        Assert.Contains("A profile is durable", skill);
        Assert.Contains("nook open-many", skill);
        Assert.Contains("nook close-others", skill);
        Assert.Contains("agent message", skill);
        Assert.Contains("agent list", skill);
        Assert.DoesNotContain("exec nook.kill", skill);
        Assert.DoesNotContain("control-token", skill);
        Assert.DoesNotContain("socket path", skill, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("manually reload", skill, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("layout.mutate", skill, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh Cove", skill, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("python3", skill, StringComparison.OrdinalIgnoreCase);
    }

    [PlatformTheory(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    [InlineData("claude-code")]
    [InlineData("codex")]
    [InlineData("omp")]
    public void ShippedScripts_AreExecutable(string adapterName)
    {
        var dir = Path.Combine(AdaptersRoot, adapterName);
        foreach (var script in new[] { "build_launch_command.sh", "build_resume_command.sh", "list_recent_sessions.sh" })
        {
            var path = Path.Combine(dir, script);
            Assert.True(File.Exists(path), $"missing script: {path}");
            var mode = File.GetUnixFileMode(path);
            Assert.True((mode & UnixFileMode.UserExecute) != 0, $"not executable: {path}");
        }
    }
    [PlatformTheory(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    [InlineData(
        "claude-code",
        "--effort",
        "high",
        "--dangerously-skip-permissions")]
    [InlineData(
        "codex",
        "--config",
        "model_reasoning_effort=\"high\"",
        "--dangerously-bypass-hook-trust")]
    [InlineData("omp", "--thinking", "high", "--hook")]
    public void ShippedLaunchAndResumeScripts_MapSelectionsAndPreserveHooks(
        string adapterName,
        string effortFlag,
        string effortValue,
        string requiredFlag)
    {
        const string flags =
            "{\"dangerouslySkipPermissions\":true,\"model\":\"model-x\",\"effort\":\"high\"}";
        var launch = RunScript(
            adapterName,
            "build_launch_command.sh",
            flags);
        var resume = RunScript(
            adapterName,
            "build_resume_command.sh",
            "session-1",
            flags);

        foreach (var arguments in new[] { launch, resume })
        {
            var modelIndex = Array.IndexOf(arguments, "--model");
            Assert.True(modelIndex >= 0);
            Assert.Equal("model-x", arguments[modelIndex + 1]);
            Assert.Contains(
                Enumerable.Range(0, arguments.Length - 1),
                index => arguments[index] == effortFlag && arguments[index + 1] == effortValue);
            Assert.Contains(requiredFlag, arguments);
        }
    }

    private static string[] RunScript(
        string adapterName,
        string scriptName,
        params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("/bin/bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(
            Path.Combine(AdaptersRoot, adapterName, scriptName));
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(
            process.ExitCode == 0,
            $"{adapterName}/{scriptName}: {stderr}");
        using var document = System.Text.Json.JsonDocument.Parse(stdout);
        return document.RootElement
            .GetProperty("command")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
    }

}
