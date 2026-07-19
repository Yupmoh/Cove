using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class AdapterContractParityTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));

    private static readonly string[] ShippedAdapters =
    [
        "claude-code",
        "codex",
        "cursor-agent",
        "hermes",
        "omp",
        "openclaw",
        "opencode",
        "pi",
    ];

    public static TheoryData<string, string, bool> ContractFixtures => new()
    {
        { "minimal", MinimalManifest, true },
        { "complete", CompleteManifest, true },
        { "empty description is schema-valid", MinimalManifest.Replace("\"description\": \"test adapter\"", "\"description\": \"\"", StringComparison.Ordinal), true },
        { "unknown root member", AddRootProperty(MinimalManifest, "\"future\": true"), false },
        { "missing sdk version", MinimalManifest.Replace("\"sdkVersion\": 2,", "", StringComparison.Ordinal), false },
        { "missing methods", MinimalManifest.Replace(",\n  \"methods\": { \"build_launch_command\": {\"script\": \"launch.sh\"} }", "", StringComparison.Ordinal), false },
        { "unknown nested member", MinimalManifest.Replace("\"commands\": [\"test-cli\"]", "\"commands\": [\"test-cli\"], \"future\": true", StringComparison.Ordinal), false },
        { "invalid sdk", MinimalManifest.Replace("\"sdkVersion\": 2", "\"sdkVersion\": 3", StringComparison.Ordinal), false },
        { "empty display name", MinimalManifest.Replace("\"displayName\": \"Test\"", "\"displayName\": \"\"", StringComparison.Ordinal), false },
        { "invalid accent", MinimalManifest.Replace("#D97757", "orange", StringComparison.Ordinal), false },
        { "invalid version", MinimalManifest.Replace("\"version\": \"1.0.0\"", "\"version\": \"latest\"", StringComparison.Ordinal), false },
        { "method with both sources", MinimalManifest.Replace("{\"script\": \"launch.sh\"}", "{\"script\": \"launch.sh\", \"static\": \"launch.json\"}", StringComparison.Ordinal), false },
        { "method with neither source", MinimalManifest.Replace("{\"script\": \"launch.sh\"}", "{}", StringComparison.Ordinal), false },
        { "invalid hook URI", CompleteManifest.Replace("cove://hooks/test/session-start", "https://example.test/hook", StringComparison.Ordinal), false },
        { "unknown hook envelope", CompleteManifest.Replace("\"sessionStartManifest\":", "\"unknownEvent\":", StringComparison.Ordinal), false },
        { "event-specific envelope kind", CompleteManifest.Replace("\"userPromptSubmit\": { \"kind\": \"hookSpecificOutput\", \"hookEventName\": \"UserPromptSubmit\", \"includeSystemMessage\": true }", "\"userPromptSubmit\": { \"kind\": \"identity\" }", StringComparison.Ordinal), false },
        { "envelope-shape-only member", CompleteManifest.Replace("\"preToolUse\": { \"kind\": \"none\" }", "\"preToolUse\": { \"kind\": \"none\", \"includeSystemMessage\": false }", StringComparison.Ordinal), false },
        { "missing hook event name", CompleteManifest.Replace(", \"hookEventName\": \"UserPromptSubmit\"", "", StringComparison.Ordinal), false },
        { "empty discovery command", CompleteManifest.Replace("[\"test-cli\", \"test\"]", "[\"\"]", StringComparison.Ordinal), false },
        { "empty well-known path", CompleteManifest.Replace("[\"/usr/local/bin\", \"~/.local/bin\"]", "[\"\"]", StringComparison.Ordinal), false },
        { "invalid discovery regex", CompleteManifest.Replace("\"(\\\\d+\\\\.\\\\d+\\\\.\\\\d+)\"", "\"[\"", StringComparison.Ordinal), false },
        { "unknown install platform", CompleteManifest.Replace("\"macos\": { \"cmd\": \"npm install -g test\", \"postInstallAuth\": false }", "\"freebsd\": { \"cmd\": \"pkg install test\" }", StringComparison.Ordinal), false },
        { "invalid icon extension", CompleteManifest.Replace("\"icon.svg\"", "\"icon.png\"", StringComparison.Ordinal), false },
        { "retention missing fields", CompleteManifest.Replace("\"fields\": [ { \"key\": \"days\", \"label\": \"History\", \"unit\": \"d\", \"default\": 30, \"recommended\": 365, \"min\": 1, \"max\": 9999 } ],", "", StringComparison.Ordinal), false },
        { "retention empty fields", CompleteManifest.Replace("[ { \"key\": \"days\", \"label\": \"History\", \"unit\": \"d\", \"default\": 30, \"recommended\": 365, \"min\": 1, \"max\": 9999 } ]", "[]", StringComparison.Ordinal), false },
        { "session extractor missing depths", CompleteManifest.Replace(", \"supportsDepths\": [\"quick\", \"standard\", \"deep\"]", "", StringComparison.Ordinal), false },
        { "session extractor duplicate depths", CompleteManifest.Replace("[\"quick\", \"standard\", \"deep\"]", "[\"quick\", \"quick\"]", StringComparison.Ordinal), false },
        { "session extractor invalid depth", CompleteManifest.Replace("[\"quick\", \"standard\", \"deep\"]", "[\"shallow\"]", StringComparison.Ordinal), false },
        { "launcher options method", CompleteManifest.Replace("{\"static\": \"launcher_options.json\"}", "{\"static\": \"\"}", StringComparison.Ordinal), false },
        { "invalid screen status", CompleteManifest.Replace("\"needs-input\"", "\"waiting\"", StringComparison.Ordinal), false },
        { "negative screen quiet period", CompleteManifest.Replace("\"quietMs\": 0", "\"quietMs\": -1", StringComparison.Ordinal), false },
    };

    [Theory]
    [MemberData(nameof(ContractFixtures))]
    public void SchemaModelAndValidator_AgreeOnContract(string name, string json, bool expected)
    {
        using var schema = LoadSchema();
        using var instance = JsonDocument.Parse(json);
        var schemaErrors = new List<string>();
        var schemaAccepts = EvaluateSchema(schema.RootElement, instance.RootElement, "$", schemaErrors);

        AdapterManifest? model = null;
        Exception? modelError = null;
        try
        {
            model = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.AdapterManifest);
        }
        catch (JsonException ex)
        {
            modelError = ex;
        }

        var (validated, validationErrors) = ManifestValidator.Parse(json);

        Assert.True(schemaAccepts == expected, $"{name}: schema errors: {string.Join("; ", schemaErrors)}");
        if (expected)
        {
            Assert.Null(modelError);
            Assert.NotNull(model);
            Assert.NotNull(validated);
            Assert.Empty(validationErrors);
        }
        else
        {
            Assert.Null(validated);
            Assert.True(modelError is not null || validationErrors.Count > 0, $"{name}: model and validator accepted invalid fixture");
        }
    }

    [Fact]
    public void SchemaAndSourceGeneratedModel_ExposeSameManifestVocabulary()
    {
        using var schema = LoadSchema();
        AssertVocabulary(schema.RootElement, AdaptersJsonContext.Default.AdapterManifest);
        AssertVocabulary(PropertySchema(schema.RootElement, "binaryDiscovery"), AdaptersJsonContext.Default.BinaryDiscovery);
        AssertVocabulary(PropertySchema(schema.RootElement, "retention"), AdaptersJsonContext.Default.AdapterRetention);
        AssertVocabulary(PropertySchema(schema.RootElement, "retention").GetProperty("properties").GetProperty("fields").GetProperty("items"), AdaptersJsonContext.Default.RetentionField);
        AssertVocabulary(PropertySchema(schema.RootElement, "sessionExtractor"), AdaptersJsonContext.Default.SessionExtractor);
        AssertVocabulary(PropertySchema(schema.RootElement, "screenState"), AdaptersJsonContext.Default.ScreenStateDeclaration, "effectiveTailBytes", "effectiveQuietMs");
        AssertVocabulary(PropertySchema(schema.RootElement, "install"), AdaptersJsonContext.Default.PlatformRecipes);
        AssertVocabulary(PropertySchema(schema.RootElement, "install").GetProperty("properties").GetProperty("macos"), AdaptersJsonContext.Default.InstallRecipe);
    }

    [Fact]
    public void AllShippedAdapters_AreSchemaValidDeserializableAndValidated()
    {
        using var schema = LoadSchema();
        var adaptersRoot = Path.Combine(RepositoryRoot, "adapters");
        foreach (var adapter in ShippedAdapters)
        {
            var path = Path.Combine(adaptersRoot, adapter, "adapter.json");
            Assert.True(File.Exists(path), $"missing manifest: {path}");
            var json = File.ReadAllText(path);
            using var instance = JsonDocument.Parse(json);
            var schemaErrors = new List<string>();
            Assert.True(EvaluateSchema(schema.RootElement, instance.RootElement, "$", schemaErrors), $"{adapter}: {string.Join("; ", schemaErrors)}");
            var model = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.AdapterManifest);
            Assert.NotNull(model);
            var errors = ManifestValidator.Validate(model!);
            Assert.Empty(errors);
        }
    }

    private static void AssertVocabulary(JsonElement schema, JsonTypeInfo typeInfo, params string[] ignoredModelProperties)
    {
        var schemaNames = schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
        var ignored = ignoredModelProperties.ToHashSet(StringComparer.Ordinal);
        var modelNames = typeInfo.Properties.Select(property => property.Name).Where(name => !ignored.Contains(name)).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(schemaNames, modelNames);
    }

    private static JsonElement PropertySchema(JsonElement schema, string property)
        => schema.GetProperty("properties").GetProperty(property);

    private static JsonDocument LoadSchema()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "schemas", "adapter.schema.json")));

    private static bool EvaluateSchema(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (schema.TryGetProperty("oneOf", out var oneOf))
        {
            var matches = 0;
            foreach (var candidate in oneOf.EnumerateArray())
            {
                var candidateErrors = new List<string>();
                if (EvaluateSchema(candidate, instance, path, candidateErrors))
                    matches++;
            }
            if (matches != 1)
                errors.Add($"{path}: expected exactly one matching shape, found {matches}");
            return matches == 1;
        }

        if (schema.TryGetProperty("type", out var type) && !MatchesType(type.GetString()!, instance))
        {
            errors.Add($"{path}: expected {type.GetString()}, got {instance.ValueKind}");
            return false;
        }

        if (schema.TryGetProperty("const", out var constant) && !JsonElement.DeepEquals(constant, instance))
            errors.Add($"{path}: value does not equal const");
        if (schema.TryGetProperty("enum", out var choices) && !choices.EnumerateArray().Any(choice => JsonElement.DeepEquals(choice, instance)))
            errors.Add($"{path}: value is not in enum");

        if (instance.ValueKind == JsonValueKind.Object)
        {
            var properties = schema.TryGetProperty("properties", out var declared) ? declared : default;
            if (schema.TryGetProperty("required", out var required))
                foreach (var name in required.EnumerateArray().Select(value => value.GetString()!))
                    if (!instance.TryGetProperty(name, out _))
                        errors.Add($"{path}.{name}: required");

            foreach (var property in instance.EnumerateObject())
            {
                if (properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(property.Name, out var childSchema))
                {
                    EvaluateSchema(childSchema, property.Value, $"{path}.{property.Name}", errors);
                    continue;
                }
                if (schema.TryGetProperty("additionalProperties", out var additional))
                {
                    if (additional.ValueKind == JsonValueKind.False)
                        errors.Add($"{path}.{property.Name}: additional property");
                    else if (additional.ValueKind == JsonValueKind.Object)
                        EvaluateSchema(additional, property.Value, $"{path}.{property.Name}", errors);
                }
            }
        }

        if (instance.ValueKind == JsonValueKind.Array)
        {
            var items = instance.EnumerateArray().ToArray();
            if (schema.TryGetProperty("minItems", out var minItems) && items.Length < minItems.GetInt32())
                errors.Add($"{path}: too few items");
            if (schema.TryGetProperty("uniqueItems", out var unique) && unique.GetBoolean())
                for (var i = 0; i < items.Length; i++)
                    for (var j = i + 1; j < items.Length; j++)
                        if (JsonElement.DeepEquals(items[i], items[j]))
                            errors.Add($"{path}: duplicate items");
            if (schema.TryGetProperty("items", out var itemSchema))
                for (var i = 0; i < items.Length; i++)
                    EvaluateSchema(itemSchema, items[i], $"{path}[{i}]", errors);
        }

        if (instance.ValueKind == JsonValueKind.String)
        {
            var value = instance.GetString()!;
            if (schema.TryGetProperty("minLength", out var minLength) && value.Length < minLength.GetInt32())
                errors.Add($"{path}: too short");
            if (schema.TryGetProperty("pattern", out var pattern) && !Regex.IsMatch(value, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                errors.Add($"{path}: pattern mismatch");
            if (schema.TryGetProperty("format", out var format) && format.GetString() == "regex")
                try { _ = new Regex(value, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)); }
                catch (ArgumentException) { errors.Add($"{path}: invalid regex"); }
        }

        if (instance.ValueKind == JsonValueKind.Number)
        {
            var value = instance.GetDecimal();
            if (schema.TryGetProperty("minimum", out var minimum) && value < minimum.GetDecimal())
                errors.Add($"{path}: below minimum");
            if (schema.TryGetProperty("maximum", out var maximum) && value > maximum.GetDecimal())
                errors.Add($"{path}: above maximum");
        }

        return errors.Count == 0;
    }

    private static bool MatchesType(string type, JsonElement instance) => type switch
    {
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "integer" => instance.ValueKind == JsonValueKind.Number && instance.TryGetInt64(out _),
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        _ => false,
    };

    private static string AddRootProperty(string json, string property)
        => json[..json.LastIndexOf('}')] + "," + property + "}";

    private const string MinimalManifest = """
    {
      "sdkVersion": 2,
      "name": "test-adapter",
      "displayName": "Test",
      "description": "test adapter",
      "accent": "#D97757",
      "binary": "test-cli",
      "version": "1.0.0",
      "methods": { "build_launch_command": {"script": "launch.sh"} },
      "binaryDiscovery": { "commands": ["test-cli"] }
    }
    """;

    private const string CompleteManifest = """
    {
      "sdkVersion": 2,
      "name": "test-adapter",
      "displayName": "Test",
      "description": "test adapter",
      "accent": "#D97757",
      "binary": "test-cli",
      "version": "1.0.0",
      "author": "Cove",
      "methods": {
        "build_launch_command": {"script": "launch.sh"},
        "launcher_options": {"static": "launcher_options.json"}
      },
      "hooks": { "session-start": "cove://hooks/test/session-start" },
      "hookEnvelopes": {
        "sessionStartManifest": { "kind": "identity" },
        "userPromptSubmit": { "kind": "hookSpecificOutput", "hookEventName": "UserPromptSubmit", "includeSystemMessage": true },
        "preToolUse": { "kind": "none" },
        "postToolUse": { "kind": "none" }
      },
      "binaryDiscovery": {
        "commands": ["test-cli", "test"],
        "wellKnownPaths": ["/usr/local/bin", "~/.local/bin"],
        "versionFlag": "--version",
        "versionRegex": "(\\d+\\.\\d+\\.\\d+)"
      },
      "install": { "macos": { "cmd": "npm install -g test", "postInstallAuth": false } },
      "skillInstallPath": "~/.test/skill.md",
      "skillsDir": "skills",
      "icon": "icon.svg",
      "retention": {
        "fields": [ { "key": "days", "label": "History", "unit": "d", "default": 30, "recommended": 365, "min": 1, "max": 9999 } ],
        "readScript": "read-retention.sh",
        "writeScript": "write-retention.sh"
      },
      "sessionExtractor": { "script": "extract-session.sh", "schemaVersion": 1, "supportsDepths": ["quick", "standard", "deep"] },
      "update": { "linux": { "cmd": "npm update -g test" } },
      "uninstall": { "windows": { "cmd": "npm uninstall -g test" } },
      "screenState": { "quietMs": 0, "tailBytes": 200000, "rules": [ { "pattern": "ready", "status": "needs-input" } ] }
    }
    """;
}
