using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cove.Adapters;

public sealed record ValidationError(string Field, string Code, string Message);

public static class ManifestValidator
{
    private static readonly Regex NameRegex = new(@"^[a-z0-9-]+$", RegexOptions.Compiled);
    private static readonly Regex AccentRegex = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private static readonly Regex SemverRegex = new(@"^\d+\.\d+\.\d+", RegexOptions.Compiled);

    public static List<ValidationError> Validate(AdapterManifest m)
    {
        var errors = new List<ValidationError>();

        if (m.SdkVersion is not 1 and not 2)
            errors.Add(new ValidationError("sdkVersion", "invalid_value", "sdkVersion must be 1 or 2"));

        if (string.IsNullOrWhiteSpace(m.Name))
            errors.Add(new ValidationError("name", "missing", "name is required"));
        else if (!NameRegex.IsMatch(m.Name))
            errors.Add(new ValidationError("name", "invalid_format", "name must match ^[a-z0-9-]+$"));

        if (string.IsNullOrWhiteSpace(m.DisplayName))
            errors.Add(new ValidationError("displayName", "missing", "displayName is required"));

        if (string.IsNullOrWhiteSpace(m.Description))
            errors.Add(new ValidationError("description", "missing", "description is required"));

        if (string.IsNullOrWhiteSpace(m.Accent))
            errors.Add(new ValidationError("accent", "missing", "accent is required"));
        else if (!AccentRegex.IsMatch(m.Accent))
            errors.Add(new ValidationError("accent", "invalid_format", "accent must be #RRGGBB"));

        if (string.IsNullOrWhiteSpace(m.Binary))
            errors.Add(new ValidationError("binary", "missing", "binary is required"));

        if (string.IsNullOrWhiteSpace(m.Version))
            errors.Add(new ValidationError("version", "missing", "version is required"));
        else if (!SemverRegex.IsMatch(m.Version))
            errors.Add(new ValidationError("version", "invalid_format", "version must be semver"));

        if (m.Methods is null || m.Methods.Count == 0)
            errors.Add(new ValidationError("methods", "missing", "methods is required and non-empty"));
        else
            foreach (var kv in m.Methods)
                if ((string.IsNullOrEmpty(kv.Value.Script) && string.IsNullOrEmpty(kv.Value.Static))
                    || (!string.IsNullOrEmpty(kv.Value.Script) && !string.IsNullOrEmpty(kv.Value.Static)))
                    errors.Add(new ValidationError($"methods.{kv.Key}", "script_xor_static", "method must have script XOR static"));

        return errors;
    }

    public static (AdapterManifest? Manifest, List<ValidationError> Errors) Parse(string json)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.AdapterManifest);
            if (manifest is null)
                return (null, [new ValidationError("root", "null", "manifest parsed to null")]);
            var errors = Validate(manifest);
            return errors.Count > 0 ? (null, errors) : (manifest, errors);
        }
        catch (JsonException ex)
        {
            return (null, [new ValidationError("root", "json_error", ex.Message)]);
        }
    }
}
