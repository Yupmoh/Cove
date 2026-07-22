using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed record ValidationError(string Field, string Code, string Message);

public static class ManifestValidator
{
    private static readonly Regex NameRegex = new(@"^[a-z0-9-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AccentRegex = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SemverRegex = new(@"^\d+\.\d+\.\d+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NpmPackageRegex = new(@"^(?:@[a-z0-9][a-z0-9._-]*/)?[a-z0-9][a-z0-9._-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BrewPackageRegex = new(@"^(?!.*(?:^|/)\.\.(?:/|$))[A-Za-z0-9][A-Za-z0-9@+._-]*(?:/[A-Za-z0-9][A-Za-z0-9@+._-]*)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> HookEnvelopeNames = new(StringComparer.Ordinal)
    {
        "sessionStartManifest", "userPromptSubmit", "preToolUse", "postToolUse",
    };
    private static readonly HashSet<string> ExtractionDepths = new(StringComparer.Ordinal)
    {
        "quick", "standard", "deep",
    };

    public static List<ValidationError> Validate(AdapterManifest manifest, ILogger? logger = null)
    {
        var errors = new List<ValidationError>();

        if (manifest.SdkVersion is < 1 or > 2)
            Add(errors, "sdkVersion", "invalid_value", "sdkVersion must be 1 or 2");
        RequiredPattern(errors, "name", manifest.Name, NameRegex, "name must match ^[a-z0-9-]+$");
        Required(errors, "displayName", manifest.DisplayName);
        if (manifest.Description is null)
            Add(errors, "description", "missing", "description is required");
        RequiredPattern(errors, "accent", manifest.Accent, AccentRegex, "accent must be #RRGGBB");
        Required(errors, "binary", manifest.Binary);
        RequiredPattern(errors, "version", manifest.Version, SemverRegex, "version must be semver");

        if (manifest.Methods is null)
        {
            Add(errors, "methods", "missing", "methods is required");
        }
        else
        {
            foreach (var (name, method) in manifest.Methods)
            {
                var hasScript = !string.IsNullOrEmpty(method.Script);
                var hasStatic = !string.IsNullOrEmpty(method.Static);
                if (hasScript == hasStatic)
                    Add(errors, $"methods.{name}", "script_xor_static", "method must have script XOR static");
            }
        }

        foreach (var (name, uri) in manifest.Hooks)
            if (!uri.StartsWith("cove://", StringComparison.Ordinal))
                Add(errors, $"hooks.{name}", "invalid_format", "hook URI must start with cove://");

        foreach (var (eventName, declaration) in manifest.HookEnvelopes)
            ValidateHookEnvelope(errors, eventName, declaration);

        if (manifest.BinaryDiscovery is { } discovery)
        {
            ValidateNonEmptyValues(errors, "binaryDiscovery.commands", discovery.Commands);
            ValidateNonEmptyValues(errors, "binaryDiscovery.wellKnownPaths", discovery.WellKnownPaths);
            OptionalNonEmpty(errors, "binaryDiscovery.versionFlag", discovery.VersionFlag);
            OptionalNonEmpty(errors, "binaryDiscovery.versionRegex", discovery.VersionRegex);
            if (!string.IsNullOrEmpty(discovery.VersionRegex))
                ValidateRegex(errors, "binaryDiscovery.versionRegex", discovery.VersionRegex);
        }

        if (manifest.PackageIdentity is { } packageIdentity)
        {
            OptionalPattern(
                errors,
                "packageIdentity.npm",
                packageIdentity.Npm,
                NpmPackageRegex,
                "npm package identity must be an exact package name");
            OptionalPattern(
                errors,
                "packageIdentity.brew",
                packageIdentity.Brew,
                BrewPackageRegex,
                "brew package identity must be an exact formula or cask name");
        }

        ValidateRecipes(errors, "install", manifest.Install);
        ValidateRecipes(errors, "update", manifest.Update);
        ValidateRecipes(errors, "uninstall", manifest.Uninstall);
        OptionalNonEmpty(errors, "skillInstallPath", manifest.SkillInstallPath);
        OptionalNonEmpty(errors, "skillsDir", manifest.SkillsDir);
        OptionalNonEmpty(errors, "icon", manifest.Icon);
        if (manifest.Icon is { Length: > 0 } icon && !icon.EndsWith(".svg", StringComparison.Ordinal))
            Add(errors, "icon", "invalid_format", "icon must end with .svg");

        if (manifest.Retention is { } retention)
        {
            if (retention.Fields is null || retention.Fields.Count == 0)
                Add(errors, "retention.fields", "missing", "retention fields must contain at least one field");
            else
                for (var i = 0; i < retention.Fields.Count; i++)
                    ValidateRetentionField(errors, i, retention.Fields[i]);
            Required(errors, "retention.readScript", retention.ReadScript);
            Required(errors, "retention.writeScript", retention.WriteScript);
        }

        if (manifest.SessionExtractor is { } extractor)
        {
            Required(errors, "sessionExtractor.script", extractor.Script);
            if (extractor.SchemaVersion < 1)
                Add(errors, "sessionExtractor.schemaVersion", "invalid_value", "schemaVersion must be at least 1");
            if (extractor.SupportsDepths is null || extractor.SupportsDepths.Count == 0)
            {
                Add(errors, "sessionExtractor.supportsDepths", "missing", "supportsDepths must contain at least one depth");
            }
            else
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < extractor.SupportsDepths.Count; i++)
                {
                    var depth = extractor.SupportsDepths[i];
                    if (!ExtractionDepths.Contains(depth))
                        Add(errors, $"sessionExtractor.supportsDepths[{i}]", "invalid_value", "unsupported extraction depth");
                    else if (!seen.Add(depth))
                        Add(errors, $"sessionExtractor.supportsDepths[{i}]", "duplicate", "extraction depths must be unique");
                }
            }
        }

        if (manifest.ScreenState is { } screen)
        {
            if (screen.QuietMs < 0)
                Add(errors, "screenState.quietMs", "invalid_value", "quietMs must be non-negative");
            if (screen.TailBytes < 0)
                Add(errors, "screenState.tailBytes", "invalid_value", "tailBytes must be non-negative");
            for (var i = 0; i < screen.Rules.Count; i++)
            {
                var rule = screen.Rules[i];
                Required(errors, $"screenState.rules[{i}].pattern", rule.Pattern);
                if (!string.IsNullOrEmpty(rule.Pattern))
                    ValidateRegex(errors, $"screenState.rules[{i}].pattern", rule.Pattern);
                if (!ScreenStateDeclaration.IsValidStatus(rule.Status))
                    Add(errors, $"screenState.rules[{i}].status", "invalid_value", $"status '{rule.Status}' is not a known agent status");
            }
        }

        var adapterName = string.IsNullOrEmpty(manifest.Name) ? "(unnamed)" : manifest.Name;
        foreach (var error in errors)
            logger?.ManifestValidationRuleFailed(adapterName, error.Field, error.Code);
        logger?.ManifestValidationSummary(adapterName, errors.Count);
        return errors;
    }

    public static (AdapterManifest? Manifest, List<ValidationError> Errors) Parse(string json, ILogger? logger = null)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.AdapterManifest);
            if (manifest is null)
            {
                logger?.ManifestParseFailed("manifest parsed to null");
                return (null, [new ValidationError("root", "null", "manifest parsed to null")]);
            }
            var errors = Validate(manifest, logger);
            return errors.Count > 0 ? (null, errors) : (manifest, errors);
        }
        catch (JsonException ex)
        {
            logger?.ManifestParseFailed(ex.Message);
            return (null, [new ValidationError("root", "json_error", ex.Message)]);
        }
    }

    private static void ValidateHookEnvelope(List<ValidationError> errors, string eventName, HookEnvelopeDeclaration declaration)
    {
        if (!HookEnvelopeNames.Contains(eventName))
        {
            Add(errors, $"hookEnvelopes.{eventName}", "unknown", "unknown hook envelope event");
            return;
        }

        var allowed = eventName == "sessionStartManifest"
            ? declaration.Kind is HookEnvelopeKind.Identity or HookEnvelopeKind.FlatAdditionalContext or HookEnvelopeKind.None or HookEnvelopeKind.HookSpecificOutput
            : declaration.Kind is HookEnvelopeKind.None or HookEnvelopeKind.HookSpecificOutput;
        if (!allowed)
            Add(errors, $"hookEnvelopes.{eventName}.kind", "invalid_value", "envelope kind is not valid for this event");
        if (declaration.Kind == HookEnvelopeKind.HookSpecificOutput && string.IsNullOrEmpty(declaration.HookEventName))
            Add(errors, $"hookEnvelopes.{eventName}.hookEventName", "missing", "hookEventName is required for hookSpecificOutput");
        if (declaration.Kind != HookEnvelopeKind.HookSpecificOutput && declaration.HookEventName is not null)
            Add(errors, $"hookEnvelopes.{eventName}.hookEventName", "invalid_value", "hookEventName is only valid for hookSpecificOutput");
        if (declaration.IncludeSystemMessage is not null
            && (eventName == "sessionStartManifest" || declaration.Kind != HookEnvelopeKind.HookSpecificOutput))
            Add(errors, $"hookEnvelopes.{eventName}.includeSystemMessage", "invalid_value", "includeSystemMessage is not valid for this envelope shape");
    }

    private static void ValidateRecipes(List<ValidationError> errors, string path, PlatformRecipes? recipes)
    {
        if (recipes?.Macos is { } macos)
            Required(errors, $"{path}.macos.cmd", macos.Cmd);
        if (recipes?.Linux is { } linux)
            Required(errors, $"{path}.linux.cmd", linux.Cmd);
        if (recipes?.Windows is { } windows)
            Required(errors, $"{path}.windows.cmd", windows.Cmd);
    }

    private static void ValidateRetentionField(List<ValidationError> errors, int index, RetentionField field)
    {
        var path = $"retention.fields[{index}]";
        Required(errors, $"{path}.key", field.Key);
        Required(errors, $"{path}.label", field.Label);
        if (field.Default < 0)
            Add(errors, $"{path}.default", "invalid_value", "default must be non-negative");
        if (field.Recommended < 1)
            Add(errors, $"{path}.recommended", "invalid_value", "recommended must be at least 1");
        if (field.Min is < 0)
            Add(errors, $"{path}.min", "invalid_value", "min must be non-negative");
        if (field.Max is < 1)
            Add(errors, $"{path}.max", "invalid_value", "max must be at least 1");
    }

    private static void ValidateNonEmptyValues(List<ValidationError> errors, string path, IReadOnlyList<string> values)
    {
        for (var i = 0; i < values.Count; i++)
            Required(errors, $"{path}[{i}]", values[i]);
    }

    private static void ValidateRegex(List<ValidationError> errors, string field, string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        catch (ArgumentException ex)
        {
            Add(errors, field, "invalid_regex", ex.Message);
        }
    }

    private static void Required(List<ValidationError> errors, string field, string? value)
    {
        if (string.IsNullOrEmpty(value))
            Add(errors, field, "missing", $"{field} is required");
    }

    private static void OptionalNonEmpty(List<ValidationError> errors, string field, string? value)
    {
        if (value is not null && value.Length == 0)
            Add(errors, field, "invalid_value", $"{field} must not be empty");
    }

    private static void RequiredPattern(List<ValidationError> errors, string field, string? value, Regex pattern, string message)
    {
        if (string.IsNullOrEmpty(value))
            Add(errors, field, "missing", $"{field} is required");
        else if (!pattern.IsMatch(value))
            Add(errors, field, "invalid_format", message);
    }

    private static void OptionalPattern(List<ValidationError> errors, string field, string? value, Regex pattern, string message)
    {
        if (value is not null && !pattern.IsMatch(value))
            Add(errors, field, "invalid_format", message);
    }

    private static void Add(List<ValidationError> errors, string field, string code, string message)
        => errors.Add(new ValidationError(field, code, message));
}
