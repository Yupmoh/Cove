using System.Text.Json;
using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class AdapterDtoTests
{
    [Fact]
    public void Manifest_RoundTrips_ThroughSourceGenJson()
    {
        var manifest = new AdapterManifest
        {
            SdkVersion = 2,
            Name = "claude-code",
            DisplayName = "Claude Code",
            Description = "Anthropic CLI",
            Accent = "#D97757",
            Binary = "claude",
            Version = "1.0.0",
            Methods = new Dictionary<string, AdapterMethod>
            {
                ["build_launch_command"] = new AdapterMethod { Script = "build_launch_command.sh" },
                ["build_resume_command"] = new AdapterMethod { Script = "build_resume_command.sh" },
                ["list_recent_sessions"] = new AdapterMethod { Script = "list_recent_sessions.sh" },
            },
            BinaryDiscovery = new BinaryDiscovery
            {
                Commands = ["claude"],
                VersionFlag = "--version",
                VersionRegex = @"(\d+\.\d+\.\d+)",
            },
            SuggestedFlags = ["--model", "sonnet"],
        };

        var json = JsonSerializer.Serialize(manifest, AdaptersJsonContext.Default.AdapterManifest);
        var deserialized = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.AdapterManifest);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.SdkVersion);
        Assert.Equal("claude-code", deserialized.Name);
        Assert.Equal("#D97757", deserialized.Accent);
        Assert.Equal("1.0.0", deserialized.Version);
        Assert.Equal(3, deserialized.Methods.Count);
        Assert.Contains(deserialized.Methods, kv => kv.Key == "build_launch_command" && kv.Value.Script == "build_launch_command.sh");
        Assert.NotNull(deserialized.BinaryDiscovery);
        Assert.Contains("claude", deserialized.BinaryDiscovery!.Commands);
    }

    [Fact]
    public void RegistryEntry_RoundTrips_ThroughSourceGenJson()
    {
        var entry = new RegistryEntry
        {
            Name = "claude-code",
            DisplayName = "Claude Code",
            Description = "Anthropic CLI",
            Accent = "#D97757",
            Binary = "claude",
            SdkVersion = 2,
            Version = "1.0.0",
            Official = true,
            Models = ["sonnet", "opus"],
            Install = new Dictionary<string, InstallRecipe>
            {
                ["darwin"] = new InstallRecipe { Cmd = "npm install -g @anthropic-ai/claude-code" },
            },
        };

        var json = JsonSerializer.Serialize(entry, AdaptersJsonContext.Default.RegistryEntry);
        var deserialized = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.RegistryEntry);

        Assert.NotNull(deserialized);
        Assert.Equal("claude-code", deserialized!.Name);
        Assert.True(deserialized.Official);
        Assert.Equal(2, deserialized.Models.Count);
        Assert.Contains(deserialized.Install, kv => kv.Key == "darwin");
    }

    [Fact]
    public void HookEvent_RoundTrips_ThroughSourceGenJson()
    {
        var evt = new HookEvent
        {
            Adapter = "claude-code",
            Event = "session-start",
            NookId = "nook-123",
            SessionId = "sess-456",
        };

        var json = JsonSerializer.Serialize(evt, AdaptersJsonContext.Default.HookEvent);
        var deserialized = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.HookEvent);

        Assert.NotNull(deserialized);
        Assert.Equal("claude-code", deserialized!.Adapter);
        Assert.Equal("session-start", deserialized.Event);
        Assert.Equal("nook-123", deserialized.NookId);
    }

    [Fact]
    public void InstalledAdapter_RoundTrips_ThroughSourceGenJson()
    {
        var installed = new InstalledAdapter
        {
            Name = "claude-code",
            Dir = "/home/.cove/adapters/claude-code",
            Manifest = new AdapterManifest
            {
                SdkVersion = 2,
                Name = "claude-code",
                DisplayName = "Claude Code",
                Description = "test",
                Accent = "#D97757",
                Binary = "claude",
                Version = "1.0.0",
                Methods = new Dictionary<string, AdapterMethod>(),
            },
            BinaryPath = "/usr/local/bin/claude",
            Version = "1.0.0",
            DetectionState = AdapterDetectionState.Detected,
        };

        var json = JsonSerializer.Serialize(installed, AdaptersJsonContext.Default.InstalledAdapter);
        var deserialized = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.InstalledAdapter);

        Assert.NotNull(deserialized);
        Assert.Equal(AdapterDetectionState.Detected, deserialized!.DetectionState);
        Assert.Equal("/usr/local/bin/claude", deserialized.BinaryPath);
    }
}
