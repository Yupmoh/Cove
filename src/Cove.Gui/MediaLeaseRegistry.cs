using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Cove.Gui;

public sealed class MediaLeaseRegistry
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(8);

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".mp4", ".m4v", ".webm", ".ogg", ".ogv", ".mov",
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg",
    };

    private readonly ConcurrentDictionary<string, Lease> _leases = new(StringComparer.Ordinal);
    private readonly TimeSpan _ttl;
    private readonly Func<DateTimeOffset> _clock;

    public MediaLeaseRegistry() : this(DefaultTtl) { }

    public MediaLeaseRegistry(TimeSpan ttl, Func<DateTimeOffset>? clock = null)
    {
        _ttl = ttl;
        _clock = clock ?? (static () => DateTimeOffset.UtcNow);
    }

    public string Issue(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !Path.IsPathRooted(filePath))
            throw new InvalidOperationException($"media lease requires an absolute path, got '{filePath}'");
        var full = Path.GetFullPath(filePath);
        if (!AllowedExtensions.Contains(Path.GetExtension(full)))
            throw new InvalidOperationException($"media lease rejected for non-media extension '{Path.GetExtension(full)}'");
        if (!File.Exists(full))
            throw new InvalidOperationException($"media lease rejected for missing file '{full}'");
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _leases[id] = new Lease(full, _clock() + _ttl);
        return id;
    }

    public bool TryResolve(string leaseId, out string filePath)
    {
        filePath = "";
        if (string.IsNullOrEmpty(leaseId) || !_leases.TryGetValue(leaseId, out var lease))
            return false;
        if (_clock() > lease.Expires)
        {
            _leases.TryRemove(leaseId, out _);
            return false;
        }
        filePath = lease.Path;
        return true;
    }

    private readonly record struct Lease(string Path, DateTimeOffset Expires);
}
