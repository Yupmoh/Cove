using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed record LaunchProfile(
    string Name,
    string Slug,
    string Adapter,
    bool IsDefault,
    string? Model,
    string? Effort,
    IReadOnlyList<string> CliArgs,
    IReadOnlyDictionary<string, string> Env,
    IReadOnlyDictionary<string, bool> Permissions,
    IReadOnlyList<string> Skills,
    string? Agent,
    int SchemaVersion);

public sealed record LaunchProfileValidationError(string Field, string Code, string Message);

public sealed partial class LaunchProfileValidator
{
    [GeneratedRegex(@"^[a-z0-9-]{1,64}$", RegexOptions.None)]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"^[a-z0-9-]{1,64}$", RegexOptions.None)]
    private static partial Regex AdapterRegex();

    public static bool IsValidSlug(string slug) => SlugRegex().IsMatch(slug);
    public static bool IsValidAdapter(string adapter) => AdapterRegex().IsMatch(adapter);

    public static List<LaunchProfileValidationError> Validate(LaunchProfile profile)
    {
        var errors = new List<LaunchProfileValidationError>();

        if (string.IsNullOrEmpty(profile.Slug) || !SlugRegex().IsMatch(profile.Slug))
            errors.Add(new LaunchProfileValidationError("slug", "invalid_slug", "slug must be kebab-case, 1-64 chars [a-z0-9-]"));

        if (string.IsNullOrEmpty(profile.Name))
            errors.Add(new LaunchProfileValidationError("name", "missing_name", "name is required"));

        if (string.IsNullOrEmpty(profile.Adapter) || !AdapterRegex().IsMatch(profile.Adapter))
            errors.Add(new LaunchProfileValidationError("adapter", "invalid_adapter", "adapter must be kebab-case, 1-64 chars [a-z0-9-]"));

        return errors;
    }
}

public sealed class LaunchProfileStore
{
    private readonly string _root;
    private readonly ILogger? _logger;

    public LaunchProfileStore(string root, ILogger? logger = null)
    {
        _root = root;
        _logger = logger;
    }

    public LaunchProfile? Load(string adapter, string slug)
    {
        if (!LaunchProfileValidator.IsValidAdapter(adapter) || !LaunchProfileValidator.IsValidSlug(slug))
        {
            _logger?.LaunchProfileLoadInvalidSlug(slug);
            return null;
        }
        var path = GetProfilePath(adapter, slug);
        if (!File.Exists(path))
            return null;
        var content = File.ReadAllText(path);
        return JsonSerializer.Deserialize(content, AdaptersJsonContext.Default.LaunchProfile);
    }

    public List<LaunchProfile> List(string adapter)
    {
        if (!LaunchProfileValidator.IsValidAdapter(adapter))
            return new List<LaunchProfile>();
        var profiles = new List<LaunchProfile>();
        var adapterDir = Path.Combine(_root, adapter);
        if (!Directory.Exists(adapterDir))
            return profiles;

        foreach (var file in Directory.EnumerateFiles(adapterDir, "*.json"))
        {
            if (Path.GetFileName(file) == ".nooks.json")
                continue;
            try
            {
                var profile = JsonSerializer.Deserialize(File.ReadAllText(file), AdaptersJsonContext.Default.LaunchProfile);
                if (profile is not null)
                    profiles.Add(profile);
            }
            catch (JsonException) { }
        }
        return profiles;
    }

    public List<LaunchProfile> ListAll()
    {
        var profiles = new List<LaunchProfile>();
        if (!Directory.Exists(_root))
            return profiles;

        foreach (var dir in Directory.EnumerateDirectories(_root))
        {
            var adapter = Path.GetFileName(dir);
            profiles.AddRange(List(adapter));
        }
        return profiles;
    }

    public void Save(LaunchProfile profile)
    {
        if (!LaunchProfileValidator.IsValidSlug(profile.Slug) || !LaunchProfileValidator.IsValidAdapter(profile.Adapter))
            throw new ArgumentException($"invalid slug or adapter: {profile.Slug} / {profile.Adapter}");

        var existing = List(profile.Adapter);
        var wasDefault = existing.FirstOrDefault(p => p.Slug == profile.Slug)?.IsDefault ?? false;

        if (profile.IsDefault)
            DemoteCurrentDefault(profile.Adapter, exceptSlug: profile.Slug);
        else if (wasDefault && existing.Count <= 1)
        {
            profile = profile with { IsDefault = true };
        }

        var path = GetProfilePath(profile.Adapter, profile.Slug);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(profile, AdaptersJsonContext.Default.LaunchProfile);
        File.WriteAllText(path, json);
    }

    public void SetDefault(string adapter, string slug)
    {
        if (!LaunchProfileValidator.IsValidAdapter(adapter) || !LaunchProfileValidator.IsValidSlug(slug))
        {
            _logger?.LaunchProfileLoadInvalidSlug(slug);
            return;
        }
        var profile = Load(adapter, slug);
        if (profile is null)
            return;
        DemoteCurrentDefault(adapter, exceptSlug: slug);
        Save(profile with { IsDefault = true });
    }

    public void Delete(string adapter, string slug)
    {
        if (!LaunchProfileValidator.IsValidAdapter(adapter) || !LaunchProfileValidator.IsValidSlug(slug))
        {
            _logger?.LaunchProfileLoadInvalidSlug(slug);
            return;
        }
        var path = GetProfilePath(adapter, slug);
        var wasDefault = false;
        if (File.Exists(path))
        {
            var profile = Load(adapter, slug);
            wasDefault = profile?.IsDefault ?? false;
            try { File.Delete(path); }
            catch (IOException ex) { _logger?.LaunchProfileDeleteFailed(slug, ex.Message); }
        }

        if (wasDefault)
        {
            var survivors = List(adapter);
            if (survivors.Count > 0)
                Save(survivors[0] with { IsDefault = true });
        }
    }

    public LaunchProfile GetDefault(string adapter)
    {
        var profiles = List(adapter);
        var def = profiles.FirstOrDefault(p => p.IsDefault);
        if (def is not null)
            return def;
        if (profiles.Count > 0)
            return profiles[0] with { IsDefault = true };
        return new LaunchProfile("Default", "default", adapter, true, null, null, Array.Empty<string>(), new Dictionary<string, string>(), new Dictionary<string, bool>(), Array.Empty<string>(), null, 1);
    }

    private void DemoteCurrentDefault(string adapter, string? exceptSlug = null)
    {
        var profiles = List(adapter);
        foreach (var p in profiles)
        {
            if (p.IsDefault && p.Slug != exceptSlug)
            {
                var demoted = p with { IsDefault = false };
                var path = GetProfilePath(p.Adapter, p.Slug);
                File.WriteAllText(path, JsonSerializer.Serialize(demoted, AdaptersJsonContext.Default.LaunchProfile));
            }
        }
    }

    private string GetProfilePath(string adapter, string slug) => Path.Combine(_root, adapter, slug + ".json");

    private string GetNooksPath(string adapter) => Path.Combine(_root, adapter, ".nooks.json");

    private NookSelectionStore LoadNooks(string adapter)
    {
        var path = GetNooksPath(adapter);
        if (!File.Exists(path))
            return new NookSelectionStore(new Dictionary<string, NookSelection>(), null);
        try
        {
            var content = File.ReadAllText(path);
            var store = JsonSerializer.Deserialize(content, AdaptersJsonContext.Default.NookSelectionStore);
            return store ?? new NookSelectionStore(new Dictionary<string, NookSelection>(), null);
        }
        catch (JsonException) { }
        return new NookSelectionStore(new Dictionary<string, NookSelection>(), null);
    }

    private void SaveNooks(string adapter, NookSelectionStore store)
    {
        var path = GetNooksPath(adapter);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(store, AdaptersJsonContext.Default.NookSelectionStore);
        File.WriteAllText(path, json);
    }

    public void SetProfileForNook(string adapter, string slug, string nookId)
    {
        if (!LaunchProfileValidator.IsValidAdapter(adapter) || !LaunchProfileValidator.IsValidSlug(slug))
        {
            _logger?.LaunchProfileLoadInvalidSlug(slug);
            return;
        }
        var nooks = LoadNooks(adapter);
        var selections = new Dictionary<string, NookSelection>(nooks.NookSelections)
        {
            [nookId] = new NookSelection(slug, DateTimeOffset.UtcNow)
        };
        SaveNooks(adapter, nooks with { NookSelections = selections, LastUsed = slug });
    }

    public string? GetProfileForNook(string adapter, string nookId)
    {
        if (!LaunchProfileValidator.IsValidAdapter(adapter))
            return null;
        var nooks = LoadNooks(adapter);
        return nooks.NookSelections.TryGetValue(nookId, out var sel) ? sel.Slug : null;
    }

    public void RecordLastUsed(string adapter, string slug)
    {
        if (!LaunchProfileValidator.IsValidAdapter(adapter) || !LaunchProfileValidator.IsValidSlug(slug))
        {
            _logger?.LaunchProfileLoadInvalidSlug(slug);
            return;
        }
        var nooks = LoadNooks(adapter);
        var selections = new Dictionary<string, NookSelection>(nooks.NookSelections);
        foreach (var (pid, sel) in nooks.NookSelections)
        {
            if (sel.Slug == slug)
                selections[pid] = sel with { LastUsedAt = DateTimeOffset.UtcNow };
        }
        SaveNooks(adapter, nooks with { NookSelections = selections, LastUsed = slug });
    }

    public string? GetLastUsed(string adapter)
    {
        if (!LaunchProfileValidator.IsValidAdapter(adapter))
            return null;
        var nooks = LoadNooks(adapter);
        return nooks.LastUsed;
    }

    public FooterChipData? GetFooterChipData(string adapter, string nookId)
    {
        var nookSlug = GetProfileForNook(adapter, nookId);
        var effectiveSlug = nookSlug ?? GetDefault(adapter).Slug;
        var profile = Load(adapter, effectiveSlug);
        if (profile is null)
            return null;
        var nooks = LoadNooks(adapter);
        DateTimeOffset? lastUsedAt = null;
        foreach (var (_, sel) in nooks.NookSelections)
        {
            if (sel.Slug == effectiveSlug)
            {
                lastUsedAt = sel.LastUsedAt;
                break;
            }
        }
        return new FooterChipData(effectiveSlug, profile.IsDefault, lastUsedAt);
    }
}
