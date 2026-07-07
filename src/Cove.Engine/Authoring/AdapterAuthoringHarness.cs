using System.Text.Json;
using Cove.Adapters;

namespace Cove.Engine.Authoring;

public sealed class AdapterAuthoringHarness
{
    private readonly string _root;

    public AdapterAuthoringHarness(string root)
    {
        _root = root;
    }

    public string Scaffold(string name, string displayName, string description)
    {
        var dir = Path.Combine(_root, name);
        if (Directory.Exists(dir))
            throw new IOException($"adapter directory already exists: {dir}");

        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "scripts"));

        var manifest = new AdapterManifest
        {
            SdkVersion = 1,
            Name = name,
            DisplayName = displayName,
            Description = description,
            Accent = "#4cc2d6",
            Binary = name,
            Version = "0.1.0",
            Methods = new Dictionary<string, AdapterMethod>
            {
                ["hooks"] = new() { Script = "scripts/hooks.sh" },
            },
            HookEnvelopes = new List<HookEnvelopeDeclaration>
            {
                new() { Event = "sessionStartManifest", Kind = HookEnvelopeKind.Identity },
            },
        };

        var json = JsonSerializer.Serialize(manifest, AdaptersJsonContext.Default.AdapterManifest);
        File.WriteAllText(Path.Combine(dir, "adapter.json"), json);

        File.WriteAllText(Path.Combine(dir, "scripts", "hooks.sh"), HOOKS_SCRIPT);

        return dir;
    }

    public IReadOnlyList<string> Validate(string adapterDir)
    {
        var errors = new List<string>();
        var manifestPath = Path.Combine(adapterDir, "adapter.json");
        if (!File.Exists(manifestPath))
        {
            errors.Add($"missing adapter.json in {adapterDir}");
            return errors;
        }
        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.AdapterManifest);
            if (manifest is null)
                errors.Add("manifest parsed to null");
        }
        catch (JsonException ex)
        {
            errors.Add($"invalid manifest JSON: {ex.Message}");
        }
        return errors;
    }

    private const string HOOKS_SCRIPT = "#!/usr/bin/env bash\nset -euo pipefail\n\nevent=\"${1:-}\"\ncase \"$event\" in\n  session-start) echo '{\"event\":\"session-start\"}' ;;\n  *) echo '{}' ;;\nesac\n";
}

public static class AdapterTestFixture
{
    public static AdapterManifest CreateMinimalManifest(string name) => new()
    {
        SdkVersion = 1,
        Name = name,
        DisplayName = name,
        Description = "test adapter",
        Accent = "#4cc2d6",
        Binary = name,
        Version = "1.0",
        Methods = new Dictionary<string, AdapterMethod>
        {
            ["hooks"] = new() { Script = "hooks.sh" },
        },
    };

    public static AdapterManifest CreateManifestWithHooks(string name) => CreateMinimalManifest(name) with
    {
        HookEnvelopes = new List<HookEnvelopeDeclaration>
        {
            new() { Event = "sessionStartManifest", Kind = HookEnvelopeKind.Identity },
            new() { Event = "userPromptSubmit", Kind = HookEnvelopeKind.HookSpecificOutput, IncludeSystemMessage = true },
            new() { Event = "preToolUse", Kind = HookEnvelopeKind.FlatAdditionalContext },
        },
    };

    public static void WriteManifestToDir(AdapterManifest manifest, string dir)
    {
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(manifest, AdaptersJsonContext.Default.AdapterManifest);
        File.WriteAllText(Path.Combine(dir, "adapter.json"), json);
    }
}
