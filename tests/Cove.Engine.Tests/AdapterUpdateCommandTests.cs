using Cove.Adapters;
using Cove.Engine.Adapters;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AdapterUpdateCommandTests
{
    private static AdapterManifest Manifest(string name, IReadOnlyDictionary<string, InstallRecipe>? install = null) => new()
    {
        Name = name,
        DisplayName = name,
        Description = "test",
        Accent = "#ffffff",
        Binary = name,
        Version = "1.0.0",
        Methods = new Dictionary<string, AdapterMethod>(),
        Install = install ?? new Dictionary<string, InstallRecipe>(),
    };

    [Fact]
    public void BundledAdapters_ResolveToPackageManagerUpdates()
    {
        Assert.Equal("npm install -g @anthropic-ai/claude-code@latest", AdapterListCommands.ResolveUpdateCommand(Manifest("claude-code")));
        Assert.Equal("npm install -g @openai/codex@latest", AdapterListCommands.ResolveUpdateCommand(Manifest("codex")));
        Assert.Equal("npm install -g @google/gemini-cli@latest", AdapterListCommands.ResolveUpdateCommand(Manifest("gemini")));
        Assert.Equal("bun install -g @oh-my-pi/pi-coding-agent@latest", AdapterListCommands.ResolveUpdateCommand(Manifest("omp")));
    }

    [Fact]
    public void ManifestInstallRecipe_WinsOverFallback()
    {
        var install = new Dictionary<string, InstallRecipe>
        {
            ["macos"] = new() { Cmd = "brew upgrade claude" },
            ["linux"] = new() { Cmd = "brew upgrade claude" },
            ["windows"] = new() { Cmd = "brew upgrade claude" },
        };
        Assert.Equal("brew upgrade claude", AdapterListCommands.ResolveUpdateCommand(Manifest("claude-code", install)));
    }

    [Fact]
    public void UnknownAdapterWithoutRecipe_HasNoUpdateCommand()
    {
        Assert.Null(AdapterListCommands.ResolveUpdateCommand(Manifest("mystery-tool")));
    }
}
