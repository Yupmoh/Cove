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
    public void NpmGlobalBinary_UpdatesThroughNpmAllowingItsInstallScripts()
    {
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("claude-code"), "/opt/homebrew/lib/node_modules/@anthropic-ai/claude-code/cli.js");
        Assert.Equal("npm install -g --allow-scripts=@anthropic-ai/claude-code @anthropic-ai/claude-code@latest", cmd);
    }

    [Fact]
    public void UnknownInstallLocation_FallsBackToNpmPackage()
    {
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("codex"), "/usr/local/bin/codex");
        Assert.Equal("npm install -g --allow-scripts=@openai/codex @openai/codex@latest", cmd);
    }

    [Fact]
    public void ManifestInstallRecipe_YieldsToDetectedProvenance()
    {
        var install = new Dictionary<string, InstallRecipe>
        {
            ["macos"] = new() { Cmd = "npm install -g opencode-ai@latest" },
            ["linux"] = new() { Cmd = "npm install -g opencode-ai@latest" },
            ["windows"] = new() { Cmd = "npm install -g opencode-ai@latest" },
        };
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("opencode", install), "/opt/homebrew/Cellar/opencode/1.18.2/bin/opencode");
        Assert.Equal("brew upgrade opencode", cmd);
    }

    [Fact]
    public void ManifestInstallRecipe_CoversUnknownProvenance()
    {
        var install = new Dictionary<string, InstallRecipe>
        {
            ["macos"] = new() { Cmd = "mise upgrade claude" },
            ["linux"] = new() { Cmd = "mise upgrade claude" },
            ["windows"] = new() { Cmd = "mise upgrade claude" },
        };
        var cmd = AdapterListCommands.ResolveUpdateCommand(Manifest("claude-code", install), "/Users/x/.claude/local/claude");
        Assert.Equal("mise upgrade claude", cmd);
    }

    [Fact]
    public void ExplicitUpdateRecipe_WinsOverProvenance()
    {
        var update = new Dictionary<string, InstallRecipe>
        {
            ["macos"] = new() { Cmd = "hermes update" },
            ["linux"] = new() { Cmd = "hermes update" },
            ["windows"] = new() { Cmd = "hermes update" },
        };
        var manifest = Manifest("hermes") with { Update = update };
        Assert.Equal("hermes update", AdapterListCommands.ResolveUpdateCommand(manifest, "/opt/homebrew/Cellar/hermes/1.0/bin/hermes"));
    }

    [Fact]
    public void ExplicitUninstallRecipe_EnablesRemovalWithoutProvenance()
    {
        var uninstall = new Dictionary<string, InstallRecipe>
        {
            ["macos"] = new() { Cmd = "hermes uninstall" },
            ["linux"] = new() { Cmd = "hermes uninstall" },
            ["windows"] = new() { Cmd = "hermes uninstall" },
        };
        var manifest = Manifest("hermes") with { Uninstall = uninstall };
        Assert.Equal("hermes uninstall", AdapterListCommands.ResolveUninstallCommand(manifest, "/Users/x/.local/bin/hermes"));
    }

    [Fact]
    public void InstallCommand_UsesRecipeNotSelfUpdater()
    {
        var install = new Dictionary<string, InstallRecipe>
        {
            ["macos"] = new() { Cmd = "curl https://cursor.com/install -fsS | bash" },
            ["linux"] = new() { Cmd = "curl https://cursor.com/install -fsS | bash" },
            ["windows"] = new() { Cmd = "curl https://cursor.com/install -fsS | bash" },
        };
        var update = new Dictionary<string, InstallRecipe>
        {
            ["macos"] = new() { Cmd = "agent update" },
            ["linux"] = new() { Cmd = "agent update" },
            ["windows"] = new() { Cmd = "agent update" },
        };
        var manifest = Manifest("cursor-agent") with { Install = install, Update = update };
        Assert.Equal("curl https://cursor.com/install -fsS | bash", AdapterListCommands.ResolveInstallCommand(manifest));
        Assert.Equal("agent update", AdapterListCommands.ResolveUpdateCommand(manifest, null));
    }

    [Fact]
    public void InstallCommand_FallsBackToKnownNpmPackage()
    {
        Assert.Equal("npm install -g --allow-scripts=@openai/codex @openai/codex@latest", AdapterListCommands.ResolveInstallCommand(Manifest("codex")));
        Assert.Null(AdapterListCommands.ResolveInstallCommand(Manifest("mystery-tool")));
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

    [Fact]
    public void UninstallBrewBinary_RemovesThroughBrew()
    {
        var cmd = AdapterListCommands.ResolveUninstallCommand(Manifest("codex"), "/opt/homebrew/Cellar/codex/0.144.4/bin/codex");
        Assert.Equal("brew uninstall codex", cmd);
    }

    [Fact]
    public void UninstallBunBinary_RemovesThroughBun()
    {
        var cmd = AdapterListCommands.ResolveUninstallCommand(Manifest("omp"), "/Users/x/.bun/install/global/node_modules/@oh-my-pi/pi-coding-agent/dist/cli.js");
        Assert.Equal("bun remove -g @oh-my-pi/pi-coding-agent", cmd);
    }

    [Fact]
    public void UninstallNpmBinary_RemovesThroughNpm()
    {
        var cmd = AdapterListCommands.ResolveUninstallCommand(Manifest("claude-code"), "/opt/homebrew/lib/node_modules/@anthropic-ai/claude-code/cli.js");
        Assert.Equal("npm uninstall -g @anthropic-ai/claude-code", cmd);
    }

    [Fact]
    public void UninstallUnknownAdapterOutsideBrew_HasNoCommand()
    {
        Assert.Null(AdapterListCommands.ResolveUninstallCommand(Manifest("mystery-tool"), "/usr/local/bin/mystery-tool"));
    }

    [Fact]
    public void UninstallNativeInstall_HasNoCommand()
    {
        Assert.Null(AdapterListCommands.ResolveUninstallCommand(Manifest("claude-code"), "/Users/x/.claude/local/claude"));
    }
}
