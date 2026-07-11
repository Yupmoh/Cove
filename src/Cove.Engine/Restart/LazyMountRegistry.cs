namespace Cove.Engine.Restart;

public interface ILazyMountRegistry
{
    bool IsMounted(string nookId);
    void Mount(string nookId);
    void Unmount(string nookId);
    IReadOnlyList<string> MountedNooks();
}

public sealed class LazyMountRegistry : ILazyMountRegistry
{
    private readonly HashSet<string> _mounted = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public bool IsMounted(string nookId)
    {
        lock (_gate)
            return _mounted.Contains(nookId);
    }

    public void Mount(string nookId)
    {
        lock (_gate)
            _mounted.Add(nookId);
    }

    public void Unmount(string nookId)
    {
        lock (_gate)
            _mounted.Remove(nookId);
    }

    public IReadOnlyList<string> MountedNooks()
    {
        lock (_gate)
            return _mounted.ToList();
    }
}
