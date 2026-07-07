using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class FixtureAdapterValidationTests
{
    private static string FixturesRoot => Path.Combine(
        Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
        "tests", "fixtures", "adapters");

    [Theory]
    [InlineData("test-v2")]
    [InlineData("test-v1")]
    [InlineData("test-hookless")]
    public void ManifestValidator_AcceptsFixtureAdapter(string adapterName)
    {
        var manifestPath = Path.Combine(FixturesRoot, adapterName, "adapter.json");
        var json = File.ReadAllText(manifestPath);
        var (manifest, errors) = ManifestValidator.Parse(json);
        Assert.True(manifest is not null, $"{adapterName} parse errors: {string.Join("; ", errors.Select(e => $"{e.Field}:{e.Code}:{e.Message}"))}");
        Assert.Empty(errors);
        Assert.Equal(adapterName, manifest!.Name);
    }

    [Fact]
    public void FixtureAdapter_TestV2_HasHooksAndBinaryDiscovery()
    {
        var manifestPath = Path.Combine(FixturesRoot, "test-v2", "adapter.json");
        var json = File.ReadAllText(manifestPath);
        var (manifest, _) = ManifestValidator.Parse(json);

        Assert.NotNull(manifest);
        Assert.Equal(2, manifest!.SdkVersion);
        Assert.True(manifest.BinaryDiscovery is not null || manifest.Methods.Count >= 3);
        Assert.Contains("build_launch_command", manifest.Methods.Keys);
        Assert.Contains("build_resume_command", manifest.Methods.Keys);
    }

    [Fact]
    public void FixtureAdapter_TestV1_UsesDetectBinaryMethod()
    {
        var manifestPath = Path.Combine(FixturesRoot, "test-v1", "adapter.json");
        var json = File.ReadAllText(manifestPath);
        var (manifest, _) = ManifestValidator.Parse(json);

        Assert.NotNull(manifest);
        Assert.Equal(1, manifest!.SdkVersion);
        Assert.Contains("detect_binary", manifest.Methods.Keys);
    }

    [Fact]
    public void FixtureAdapter_TestHookless_HasNoHooksBlock()
    {
        var manifestPath = Path.Combine(FixturesRoot, "test-hookless", "adapter.json");
        var json = File.ReadAllText(manifestPath);

        Assert.DoesNotContain("\"hooks\"", json);

        var (manifest, _) = ManifestValidator.Parse(json);
        Assert.NotNull(manifest);
        Assert.DoesNotContain("stop", manifest!.Methods.Keys);
    }
}
