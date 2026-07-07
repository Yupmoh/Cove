namespace Cove.Engine.Restart;

public interface ILazyMountRegistry
{
    bool IsMounted(string paneId);
    void Mount(string paneId);
    void Unmount(string paneId);
    IReadOnlyList<string> MountedPanes();
}

public sealed class LazyMountRegistry : ILazyMountRegistry
{
    private readonly HashSet<string> _mounted = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public bool IsMounted(string paneId)
    {
        lock (_gate)
            return _mounted.Contains(paneId);
    }

    public void Mount(string paneId)
    {
        lock (_gate)
            _mounted.Add(paneId);
    }

    public void Unmount(string paneId)
    {
        lock (_gate)
            _mounted.Remove(paneId);
    }

    public IReadOnlyList<string> MountedPanes()
    {
        lock (_gate)
            return _mounted.ToList();
    }
}
