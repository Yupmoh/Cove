using Cove.Platform;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed class AdapterManifestStore
{
    private readonly string _adaptersRoot;
    private readonly ILogger? _logger;
    private readonly IPlatformFileSystem _fileSystem;
    private readonly Dictionary<string, (AdapterManifest Manifest, DateTimeOffset LastWrite)> _cache = new();
    private readonly object _lock = new();

    public AdapterManifestStore(
        string adaptersRoot,
        ILogger? logger = null,
        IPlatformFileSystem? fileSystem = null)
    {
        _adaptersRoot = adaptersRoot;
        _logger = logger;
        _fileSystem = fileSystem ?? SystemPlatformFileSystem.Instance;
    }

    public string AdaptersRoot => _adaptersRoot;

    public string ResolveDir(string adapter) => Path.Combine(_adaptersRoot, adapter);

    public void Invalidate(string adapter)
    {
        lock (_lock)
            _cache.Remove(adapter);
    }

    public AdapterManifest? Load(string adapter)
    {
        if (!LaunchProfileValidator.IsValidAdapter(adapter))
        {
            _logger?.ManifestLoadInvalidAdapter(adapter);
            return null;
        }
        var manifestPath = Path.Combine(ResolveDir(adapter), "adapter.json");
        if (!_fileSystem.FileExists(manifestPath))
        {
            _logger?.ManifestNotFound(adapter, manifestPath);
            return null;
        }

        var lastWrite = _fileSystem.GetLastWriteTimeUtc(manifestPath);
        lock (_lock)
        {
            if (_cache.TryGetValue(adapter, out var cached) && cached.LastWrite == lastWrite)
            {
                _logger?.ManifestCacheHit(adapter);
                return cached.Manifest;
            }
        }

        try
        {
            var json = _fileSystem.ReadAllText(manifestPath);
            var (manifest, errors) = ManifestValidator.Parse(json, _logger);
            if (manifest is null)
            {
                var error = errors.Count > 0 ? $"{errors[0].Field}:{errors[0].Code}" : "unknown validation error";
                _logger?.ManifestLoadFailedAt(adapter, manifestPath, error);
                return null;
            }
            lock (_lock)
            {
                _cache[adapter] = (manifest, lastWrite);
            }
            _logger?.ManifestLoaded(adapter, manifestPath);
            return manifest;
        }
        catch (IOException ex)
        {
            _logger?.ManifestLoadFailedAt(adapter, manifestPath, ex.Message);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.ManifestLoadFailedAt(adapter, manifestPath, ex.Message);
            return null;
        }
    }

    public IReadOnlyList<AdapterManifest> LoadAll()
    {
        var manifests = new List<AdapterManifest>();
        if (!_fileSystem.DirectoryExists(_adaptersRoot))
            return manifests;
        foreach (var dir in _fileSystem.EnumerateFileSystemEntries(_adaptersRoot))
        {
            if (!_fileSystem.DirectoryExists(dir))
                continue;
            var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(name))
            {
                _logger?.ManifestDirSkippedEmptyName(dir);
                continue;
            }
            var manifest = Load(name);
            if (manifest is not null)
                manifests.Add(manifest);
        }
        return manifests;
    }
}
