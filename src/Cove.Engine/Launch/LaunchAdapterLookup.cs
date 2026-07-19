using Cove.Adapters;

namespace Cove.Engine.Launch;

public sealed record LaunchAdapter(
    AdapterManifest Manifest,
    string Directory);

public interface ILaunchAdapterLookup
{
    LaunchAdapter? Find(string adapterName);
}

public sealed class LaunchAdapterLookup(
    AdapterManifestStore manifestStore) : ILaunchAdapterLookup
{
    public LaunchAdapter? Find(string adapterName)
    {
        var manifest = manifestStore.Load(adapterName);
        return manifest is null
            ? null
            : new LaunchAdapter(
                manifest,
                manifestStore.ResolveDir(adapterName));
    }
}
