using Cove.Platform.Pty;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Nooks;

public sealed class PtyPoolEntry(IPtySession session, System.DateTimeOffset warmedAt)
{
    public IPtySession Session { get; } = session;
    public System.DateTimeOffset WarmedAt { get; } = warmedAt;
    public bool IsClaimed { get; set; }
}

public sealed class PtyPool
{
    private readonly IPtyHost _host;
    private readonly ILogger _logger;
    private readonly Dictionary<string, Queue<PtyPoolEntry>> _pools = new();
    private readonly object _lock = new();
    private readonly int _maxPoolSize;
    private readonly TimeSpan _maxWarmAge;

    public PtyPool(IPtyHost host, ILogger? logger = null, int maxPoolSize = 3, TimeSpan? maxWarmAge = null)
    {
        _host = host;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _maxPoolSize = maxPoolSize;
        _maxWarmAge = maxWarmAge ?? TimeSpan.FromSeconds(30);
    }

    public int PoolSize(string poolKey)
    {
        lock (_lock)
        {
            if (!_pools.TryGetValue(poolKey, out var queue))
                return 0;
            return queue.Count(e => !e.IsClaimed);
        }
    }

    public void PreWarm(string poolKey, PtySpawnRequest request)
    {
        if (string.IsNullOrWhiteSpace(poolKey))
        {
            _logger.LogWarning("pty-pool: pool key required");
            return;
        }

        lock (_lock)
        {
            if (!_pools.TryGetValue(poolKey, out var queue))
            {
                queue = new Queue<PtyPoolEntry>();
                _pools[poolKey] = queue;
            }

            var available = 0;
            foreach (var e in queue)
                if (!e.IsClaimed) available++;

            if (available >= _maxPoolSize)
            {
                _logger.LogDebug("pty-pool: pool {key} already at capacity {max}", poolKey, _maxPoolSize);
                return;
            }

            try
            {
                var session = _host.Spawn(request);
                var entry = new PtyPoolEntry(session, System.DateTimeOffset.UtcNow);
                queue.Enqueue(entry);
                _logger.LogInformation("pty-pool: pre-warmed session for pool {key} (size now {size})", poolKey, available + 1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "pty-pool: failed to pre-warm session for pool {key}", poolKey);
            }
        }
    }

    public IPtySession? TryAcquire(string poolKey)
    {
        if (string.IsNullOrWhiteSpace(poolKey))
        {
            _logger.LogWarning("pty-pool: pool key required for acquire");
            return null;
        }

        lock (_lock)
        {
            if (!_pools.TryGetValue(poolKey, out var queue) || queue.Count == 0)
                return null;

            while (queue.Count > 0)
            {
                var entry = queue.Dequeue();
                if (entry.IsClaimed)
                    continue;
                if (entry.Session.HasExited)
                {
                    _logger.LogWarning("pty-pool: discarding exited pre-warmed session in pool {key}", poolKey);
                    entry.Session.Dispose();
                    continue;
                }
                if (System.DateTimeOffset.UtcNow - entry.WarmedAt > _maxWarmAge)
                {
                    _logger.LogWarning("pty-pool: discarding stale pre-warmed session in pool {key} (age {age}s)", poolKey, (System.DateTimeOffset.UtcNow - entry.WarmedAt).TotalSeconds);
                    entry.Session.Dispose();
                    continue;
                }
                entry.IsClaimed = true;
                _logger.LogInformation("pty-pool: acquired pre-warmed session from pool {key}", poolKey);
                return entry.Session;
            }
            return null;
        }
    }

    public IPtySession AcquireOrSpawn(string poolKey, PtySpawnRequest request)
    {
        var pooled = TryAcquire(poolKey);
        if (pooled is not null)
            return pooled;

        _logger.LogInformation("pty-pool: no pre-warmed session in pool {key}, spawning on-demand", poolKey);
        return _host.Spawn(request);
    }

    public void DrainPool(string poolKey)
    {
        lock (_lock)
        {
            if (!_pools.TryGetValue(poolKey, out var queue))
                return;

            while (queue.Count > 0)
            {
                var entry = queue.Dequeue();
                if (!entry.IsClaimed)
                {
                    try { entry.Session.Dispose(); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "pty-pool: error disposing session in pool {key}", poolKey);
                    }
                }
            }
            _pools.Remove(poolKey);
            _logger.LogInformation("pty-pool: drained pool {key}", poolKey);
        }
    }

    public void DrainAll()
    {
        lock (_lock)
        {
            foreach (var key in _pools.Keys.ToList())
                DrainPool(key);
        }
    }
}
