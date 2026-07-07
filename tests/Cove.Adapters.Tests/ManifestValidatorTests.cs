using System.Text.Json;
using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class ManifestValidatorTests
{
    private static readonly string[] RequiredMethods =
    [
        "build_launch_command", "build_resume_command", "list_recent_sessions"
    ];

    private static AdapterManifest ValidManifest(string name = "claude-code", int sdk = 2) => new()
    {
        SdkVersion = sdk,
        Name = name,
        DisplayName = "Claude Code",
        Description = "Anthropic CLI",
        Accent = "#D97757",
        Binary = "claude",
        Version = "1.0.0",
        Methods = RequiredMethods.ToDictionary(m => m, m => (AdapterMethod)new AdapterMethod { Script = $"{m}.sh" }),
    };

    private static List<ValidationError> Validate(AdapterManifest m) => ManifestValidator.Validate(m);

    [Fact]
    public void ValidManifest_HasNoErrors()
    {
        var errors = Validate(ValidManifest());
        Assert.Empty(errors);
    }

    [Fact]
    public void SdkVersion_MustBe1or2()
    {
        var errors = Validate(ValidManifest() with { SdkVersion = 3 });
        Assert.Contains(errors, e => e.Field == "sdkVersion" && e.Code == "invalid_value");
    }

    [Fact]
    public void SdkVersion1_IsAccepted()
    {
        var errors = Validate(ValidManifest(sdk: 1));
        Assert.Empty(errors);
    }

    [Fact]
    public void Name_MustMatchRegex()
    {
        var errors = Validate(ValidManifest() with { Name = "Claude Code!" });
        Assert.Contains(errors, e => e.Field == "name" && e.Code == "invalid_format");
    }

    [Fact]
    public void Accent_MustBeHexColor()
    {
        var errors = Validate(ValidManifest() with { Accent = "red" });
        Assert.Contains(errors, e => e.Field == "accent" && e.Code == "invalid_format");
    }

    [Fact]
    public void Version_MustBeSemver()
    {
        var errors = Validate(ValidManifest() with { Version = "v1" });
        Assert.Contains(errors, e => e.Field == "version" && e.Code == "invalid_format");
    }

    [Fact]
    public void Method_MustHaveScriptXorStatic_NotBoth()
    {
        var m = ValidManifest();
        var methods = new Dictionary<string, AdapterMethod>(m.Methods)
        {
            ["build_launch_command"] = new AdapterMethod { Script = "a.sh", Static = "a.json" }
        };
        var errors = Validate(m with { Methods = methods });
        Assert.Contains(errors, e => e.Field == "methods.build_launch_command" && e.Code == "script_xor_static");
    }

    [Fact]
    public void Method_MustHaveScriptXorStatic_Neither()
    {
        var m = ValidManifest();
        var methods = new Dictionary<string, AdapterMethod>(m.Methods)
        {
            ["build_launch_command"] = new AdapterMethod()
        };
        var errors = Validate(m with { Methods = methods });
        Assert.Contains(errors, e => e.Field == "methods.build_launch_command" && e.Code == "script_xor_static");
    }

    [Fact]
    public void MissingRequiredField_Name()
    {
        var m = ValidManifest() with { Name = "" };
        var errors = Validate(m);
        Assert.Contains(errors, e => e.Field == "name" && e.Code == "missing");
    }

    [Fact]
    public void MissingRequiredField_Methods()
    {
        var m = ValidManifest() with { Methods = new Dictionary<string, AdapterMethod>() };
        var errors = Validate(m);
        Assert.Contains(errors, e => e.Field == "methods" && e.Code == "missing");
    }

    [Fact]
    public void Parse_RoundTripsValidJson()
    {
        var json = """
        {
          "sdkVersion": 2,
          "name": "codex",
          "displayName": "Codex",
          "description": "OpenAI CLI",
          "accent": "#10A37F",
          "binary": "codex",
          "version": "0.1.0",
          "methods": {
            "build_launch_command": { "script": "build_launch_command.sh" },
            "build_resume_command": { "script": "build_resume_command.sh" },
            "list_recent_sessions": { "script": "list_recent_sessions.sh" }
          }
        }
        """;
        var (manifest, errors) = ManifestValidator.Parse(json);
        Assert.Empty(errors);
        Assert.NotNull(manifest);
        Assert.Equal("codex", manifest!.Name);
    }

    [Fact]
    public void Parse_AccumulatesErrors_ForMalformedJson()
    {
        var json = """
        {
          "sdkVersion": 5,
          "name": "Bad Name!",
          "displayName": "",
          "description": "",
          "accent": "blue",
          "binary": "",
          "version": "not-semver",
          "methods": {}
        }
        """;
        var (manifest, errors) = ManifestValidator.Parse(json);
        Assert.Null(manifest);
        Assert.True(errors.Count >= 5);
    }
}
