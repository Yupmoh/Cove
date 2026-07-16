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
    public void BrewCellarBinary_UpdatesThroughBrew()
    {
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("codex"), "/opt/homebrew/Cellar/codex/0.144.4/bin/codex");
        Assert.Equal("brew upgrade codex", cmd);
    }

    [Fact]
    public void BrewCaskBinary_UpdatesThroughBrew()
    {
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("claude-code"), "/opt/homebrew/Caskroom/claude-code/2.1.198/claude");
        Assert.Equal("brew upgrade claude-code", cmd);
    }

    [Fact]
    public void GeminiBrewBinary_UsesItsFormulaName()
    {
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("gemini"), "/usr/local/Cellar/gemini-cli/1.0.0/bin/gemini");
        Assert.Equal("brew upgrade gemini-cli", cmd);
    }

    [Fact]
    public void RenamedBrewFormula_UsesThePathSegmentNotTheAdapterName()
    {
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("claude-code"), "/opt/homebrew/Cellar/claude-code-nightly/2.0.0/bin/claude");
        Assert.Equal("brew upgrade claude-code-nightly", cmd);
    }

    [Fact]
    public void MalformedCellarPath_FallsBackToTheKnownFormulaName()
    {
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("gemini"), "/opt/homebrew/Cellar/");
        Assert.Equal("brew upgrade gemini-cli", cmd);
    }

    [Fact]
    public void BunGlobalBinary_UpdatesThroughBun()
    {
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("omp"), "/Users/x/.bun/install/global/node_modules/@oh-my-pi/pi-coding-agent/dist/cli.js");
        Assert.Equal("bun install -g @oh-my-pi/pi-coding-agent@latest", cmd);
    }

    [Fact]
    public void NpmGlobalBinary_UpdatesThroughNpm()
    {
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("claude-code"), "/opt/homebrew/lib/node_modules/@anthropic-ai/claude-code/cli.js");
        Assert.Equal("npm install -g @anthropic-ai/claude-code@latest", cmd);
    }

    [Fact]
    public void UnknownInstallLocation_FallsBackToNpmPackage()
    {
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("codex"), "/usr/local/bin/codex");
        Assert.Equal("npm install -g @openai/codex@latest", cmd);
    }

    [Fact]
    public void ManifestInstallRecipe_WinsOverEverything()
    {
        var install = new Dictionary<string, InstallRecipe>
        {
            ["macos"] = new() { Cmd = "mise upgrade claude" },
            ["linux"] = new() { Cmd = "mise upgrade claude" },
            ["windows"] = new() { Cmd = "mise upgrade claude" },
        };
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("claude-code", install), "/opt/homebrew/Cellar/claude/1/bin/claude");
        Assert.Equal("mise upgrade claude", cmd);
    }

    [Fact]
    public void UnknownAdapterOutsideBrew_HasNoUpdateCommand()
    {
        Assert.Null(AdapterListCommands.ResolveUpdateCommand(Manifest("mystery-tool"), "/usr/local/bin/mystery-tool"));
    }

    [Fact]
    public void UnknownAdapterInsideBrew_StillUpdatesThroughBrew()
    {
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("mystery-tool"), "/opt/homebrew/Cellar/mystery-tool/1.0/bin/mystery-tool");
        Assert.Equal("brew upgrade mystery-tool", cmd);
    }
}
