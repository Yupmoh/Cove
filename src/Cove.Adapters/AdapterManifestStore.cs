using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed class AdapterManifestStore
{
    private readonly string _adaptersRoot;
    private readonly ILogger? _logger;
    private readonly Dictionary<string, (AdapterManifest Manifest, DateTimeOffset LastWrite)> _cache = new();
    private readonly object _lock = new();

    public AdapterManifestStore(string adaptersRoot, ILogger? logger = null)
    {
        _adaptersRoot = adaptersRoot;
        _logger = logger;
    }

    public string ResolveDir(string adapter) => Path.Combine(_adaptersRoot, adapter);

    public AdapterManifest? Load(string adapter)
    {
        if (!LaunchProfileValidator.IsValidAdapter(adapter))
        {
            _logger?.ManifestLoadInvalidAdapter(adapter);
            return null;
        }
        var manifestPath = Path.Combine(ResolveDir(adapter), "adapter.json");
        var fileInfo = new FileInfo(manifestPath);
        if (!fileInfo.Exists)
            return null;

        var lastWrite = fileInfo.LastWriteTimeUtc;
        lock (_lock)
        {
            if (_cache.TryGetValue(adapter, out var cached) && cached.LastWrite == lastWrite)
                return cached.Manifest;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            var parsed = JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.AdapterManifest);
            if (parsed is null)
            {
                _logger?.ManifestParsedNull(adapter);
                return null;
            }
            var manifest = NormalizeCollections(parsed);
            lock (_lock)
            {
                _cache[adapter] = (manifest, lastWrite);
            }
            return manifest;
        }
        catch (JsonException ex)
        {
            _logger?.ManifestLoadFailed(adapter, ex.Message);
            return null;
        }
        catch (IOException ex)
        {
            _logger?.ManifestLoadFailed(adapter, ex.Message);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.ManifestLoadFailed(adapter, ex.Message);
            return null;
        }
    }

    private static AdapterManifest NormalizeCollections(AdapterManifest manifest) => manifest with
    {
        Hooks = manifest.Hooks ?? new Dictionary<string, string>(),
        HookEnvelopes = manifest.HookEnvelopes ?? new Dictionary<string, HookEnvelopeDeclaration>(),
        Install = manifest.Install ?? new Dictionary<string, InstallRecipe>(),
        WellKnownPaths = manifest.WellKnownPaths ?? [],
        SuggestedFlags = manifest.SuggestedFlags ?? [],
    };

    public IReadOnlyList<AdapterManifest> LoadAll()
    {
        var manifests = new List<AdapterManifest>();
        if (!Directory.Exists(_adaptersRoot))
            return manifests;
        foreach (var dir in Directory.EnumerateDirectories(_adaptersRoot))
        {
            var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(name))
                continue;
            var manifest = Load(name);
            if (manifest is not null)
                manifests.Add(manifest);
        }
        return manifests;
    }
}
