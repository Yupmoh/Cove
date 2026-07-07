using System.Text.Json;
using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class GoldenRegistryParseTests
{
    private static readonly string FixturePath = Path.Combine(
        Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
        "tests", "fixtures", "registry", "golden-registry.json");

    [Fact]
    public void GoldenRegistry_ParsesAllFieldsWithoutLoss()
    {
        var json = File.ReadAllText(FixturePath);
        var registry = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.Registry);

        Assert.NotNull(registry);
        Assert.True(registry!.Adapters.Count >= 2);

        var first = registry.Adapters[0];
        Assert.False(string.IsNullOrEmpty(first.Name));
        Assert.False(string.IsNullOrEmpty(first.DisplayName));
        Assert.False(string.IsNullOrEmpty(first.Accent));
        Assert.False(string.IsNullOrEmpty(first.Binary));
        Assert.False(string.IsNullOrEmpty(first.Version));
        Assert.False(string.IsNullOrEmpty(first.IconSvg));
        Assert.NotNull(first.MinAppVersion);
        Assert.NotEmpty(first.Platforms);
        Assert.NotEmpty(first.Models);
        Assert.NotNull(first.Install);

        var recipe = first.Install.Values.First();
        Assert.False(string.IsNullOrEmpty(recipe.Cmd));
    }

    [Fact]
    public void GoldenRegistry_IconSvgIsInlineMarkup()
    {
        var json = File.ReadAllText(FixturePath);
        var registry = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.Registry);

        Assert.NotNull(registry);
        foreach (var entry in registry!.Adapters)
        {
            Assert.StartsWith("<svg", entry.IconSvg ?? "");
            Assert.EndsWith("</svg>", entry.IconSvg ?? "");
        }
    }

    [Fact]
    public void GoldenRegistry_PlatformsAreValidOsIds()
    {
        var json = File.ReadAllText(FixturePath);
        var registry = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.Registry);

        Assert.NotNull(registry);
        foreach (var entry in registry!.Adapters)
            foreach (var platform in entry.Platforms)
                Assert.Contains(platform, new[] { "macos", "linux", "windows" });
    }
}
