using System.Text.Json;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Adapters;

public sealed class AdapterEnvStore
{
    private readonly string _root;
    private readonly ILogger? _logger;

    public AdapterEnvStore(string root, ILogger? logger = null)
    {
        _root = root;
        _logger = logger;
    }

    public event Action<string>? EnvSaved;

    public List<AdapterEnvVar> Load(string adapter)
    {
        var path = GetPath(adapter);
        if (!File.Exists(path))
            return new List<AdapterEnvVar>();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, AdaptersJsonContext.Default.ListAdapterEnvVar) ?? new List<AdapterEnvVar>();
        }
        catch (JsonException ex)
        {
            _logger?.EnvStoreLoadFailed(adapter, ex.Message);
            return new List<AdapterEnvVar>();
        }
    }

    public void Save(string adapter, List<AdapterEnvVar> entries)
    {
        if (!LaunchProfileValidator.IsValidAdapter(adapter))
        {
            _logger?.EnvStoreSaveRejectedInvalidAdapter(adapter);
            return;
        }
        var path = GetPath(adapter);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(entries, AdaptersJsonContext.Default.ListAdapterEnvVar);
        File.WriteAllText(path, json);
        EnvSaved?.Invoke(adapter);
    }

    public List<string> ListAdapters()
    {
        var adapters = new List<string>();
        if (!Directory.Exists(_root))
            return adapters;
        foreach (var file in Directory.EnumerateFiles(_root, "*.json"))
            adapters.Add(Path.GetFileNameWithoutExtension(file));
        return adapters;
    }

    public void Delete(string adapter)
    {
        var path = GetPath(adapter);
        if (File.Exists(path))
        {
            try { File.Delete(path); }
            catch (IOException ex) { _logger?.EnvStoreDeleteFailed(adapter, ex.Message); }
        }
    }

    private string GetPath(string adapter) => Path.Combine(_root, adapter + ".json");
}
