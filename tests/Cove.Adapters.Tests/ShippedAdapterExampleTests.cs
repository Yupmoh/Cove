using Cove.Adapters;
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
}
