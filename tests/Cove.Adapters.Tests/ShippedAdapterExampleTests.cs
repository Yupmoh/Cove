using Cove.Adapters;
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
    [InlineData("claude-code")]
    [InlineData("codex")]
    [InlineData("omp")]
    public void ShippedAdapter_ScriptsExistAndExecutable(string adapterName)
    {
        if (System.OperatingSystem.IsWindows()) return;
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
