using System.Text.Json;
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

    [Fact]
    public void EmptyMethods_SchemaAndValidatorBothAccept()
    {
        using var schema = LoadSchema();
        var methodsSchema = schema.RootElement.GetProperty("properties").GetProperty("methods");
        var schemaAccepts = !methodsSchema.TryGetProperty("minProperties", out var minimum)
            || minimum.GetInt32() == 0;

        var (manifest, errors) = ManifestValidator.Parse(ManifestJson());
        var validatorAccepts = manifest is not null && errors.Count == 0;

        Assert.True(schemaAccepts);
        Assert.Equal(schemaAccepts, validatorAccepts);
        Assert.Empty(manifest!.Methods);
    }

    [Theory]
    [InlineData(2500, 4096)]
    [InlineData(0, 0)]
    [InlineData(100, 200000)]
    public void ScreenStateNumericValues_SchemaAndValidatorBothAccept(int quietMs, int tailBytes)
    {
        using var schema = LoadSchema();
        var screenSchema = schema.RootElement.GetProperty("properties").GetProperty("screenState").GetProperty("properties");
        var quietMin = screenSchema.GetProperty("quietMs").GetProperty("minimum").GetInt32();
        var tailMin = screenSchema.GetProperty("tailBytes").GetProperty("minimum").GetInt32();
        var schemaAccepts = quietMs >= quietMin && tailBytes >= tailMin
            && !screenSchema.GetProperty("quietMs").TryGetProperty("maximum", out _)
            && !screenSchema.GetProperty("tailBytes").TryGetProperty("maximum", out _);

        var screenState = $$""", "screenState": { "quietMs": {{quietMs}}, "tailBytes": {{tailBytes}}, "rules": [] }""";
        var (manifest, errors) = ManifestValidator.Parse(ManifestJson(screenState));
        var validatorAccepts = manifest is not null && !errors.Any(e => e.Field is "screenState.quietMs" or "screenState.tailBytes");

        Assert.True(schemaAccepts);
        Assert.Equal(schemaAccepts, validatorAccepts);
    }

    [Theory]
    [InlineData("UserPromptSubmit", true)]
    [InlineData(" ", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HookSpecificOutputEventName_SchemaAndValidatorHaveParity(string? hookEventName, bool expected)
    {
        using var schema = LoadSchema();
        var declarationSchema = HookSpecificOutputSchema(schema.RootElement, "userPromptSubmit");
        var required = declarationSchema.GetProperty("required")
            .EnumerateArray()
            .Any(value => value.GetString() == "hookEventName");
        var minimumLength = declarationSchema.GetProperty("properties")
            .GetProperty("hookEventName")
            .GetProperty("minLength")
            .GetInt32();
        var schemaAccepts = (!required || hookEventName is not null)
            && (hookEventName is null || hookEventName.Length >= minimumLength);

        var eventNameProperty = hookEventName is null
            ? ""
            : $", \"hookEventName\": {JsonSerializer.Serialize(hookEventName)}";
        var hookEnvelopes = $", \"hookEnvelopes\": {{ \"userPromptSubmit\": {{ \"kind\": \"hookSpecificOutput\"{eventNameProperty} }} }}";
        var (manifest, errors) = ManifestValidator.Parse(ManifestJson(hookEnvelopes));
        var validatorAccepts = manifest is not null && errors.Count == 0;

        Assert.Equal(expected, schemaAccepts);
        Assert.Equal(schemaAccepts, validatorAccepts);
        if (expected)
            Assert.Equal(hookEventName, manifest!.HookEnvelopes["userPromptSubmit"].HookEventName);
        else
            Assert.Contains(errors, error =>
                error.Field == "hookEnvelopes.userPromptSubmit.hookEventName"
                && error.Code == "missing");
    }

    [Fact]
    public void AllEightShippedAdapters_Validate()
    {
        var adaptersRoot = Path.Combine(RepositoryRoot, "adapters");
        foreach (var adapter in ShippedAdapters)
        {
            var path = Path.Combine(adaptersRoot, adapter, "adapter.json");
            Assert.True(File.Exists(path), $"missing manifest: {path}");
            var (manifest, errors) = ManifestValidator.Parse(File.ReadAllText(path));
            Assert.True(manifest is not null, $"{adapter}: {string.Join("; ", errors.Select(error => $"{error.Field}:{error.Code}:{error.Message}"))}");
        }
    }

    private static JsonDocument LoadSchema()
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "schemas", "adapter.schema.json")));

    private static JsonElement HookSpecificOutputSchema(JsonElement schema, string eventName)
        => schema.GetProperty("properties")
            .GetProperty("hookEnvelopes")
            .GetProperty("properties")
            .GetProperty(eventName)
            .GetProperty("oneOf")
            .EnumerateArray()
            .Single(candidate => candidate.GetProperty("properties")
                .GetProperty("kind")
                .GetProperty("const")
                .GetString() == "hookSpecificOutput");

    private static string ManifestJson(string additionalProperties = "") => $$"""
        {
          "sdkVersion": 2,
          "name": "contract-test",
          "displayName": "Contract Test",
          "description": "Contract parity fixture",
          "accent": "#123456",
          "binary": "contract-test",
          "version": "1.0.0",
          "methods": {}{{additionalProperties}}
        }
        """;
}
