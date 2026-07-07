using Microsoft.Extensions.Logging;

namespace Cove.Persistence;

public sealed class StateStore : IDisposable
{
    private sealed class Registration
    {
        public required string Path { get; init; }
        public required Func<byte[]> Serialize { get; init; }
        public required Func<bool> IsHydrated { get; init; }
    }

    private readonly ILogger _logger;
    private readonly string _journalDir;
    private readonly TimeSpan _debounce;
    private readonly int _journalKeep;
    private readonly object _gate = new();
    private readonly Dictionary<string, Registration> _registrations = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirty = new(StringComparer.Ordinal);
    private readonly Timer _timer;
    private bool _disposed;

    public StateStore(string journalDir, ILogger logger, TimeSpan? debounce = null, int journalKeep = 10)
    {
        _journalDir = journalDir;
        _logger = logger;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(750);
        _journalKeep = journalKeep;
        _timer = new Timer(_ => SafeFlush(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Register(string fileKey, string path, Func<byte[]> serialize, Func<bool> isHydrated)
    {
        lock (_gate)
            _registrations[fileKey] = new Registration { Path = path, Serialize = serialize, IsHydrated = isHydrated };
    }

    public void MarkDirty(string fileKey)
    {
        lock (_gate)
        {
            if (!_registrations.ContainsKey(fileKey))
            {
                _logger.MarkDirtyUnregistered(fileKey);
                return;
            }
            _dirty.Add(fileKey);
            if (!_disposed)
                _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    public void Flush()
    {
        List<(string Key, Registration Reg)> ready = new();
        lock (_gate)
        {
            List<string> deferred = new();
            foreach (var key in _dirty)
            {
                if (!_registrations.TryGetValue(key, out var reg))
                    continue;
                if (reg.IsHydrated())
                    ready.Add((key, reg));
                else
                    deferred.Add(key);
            }
            _dirty.Clear();
            foreach (var key in deferred)
                _dirty.Add(key);
        }

        foreach (var (key, reg) in ready)
        {
            try
            {
                var bytes = reg.Serialize();
                AtomicJsonStore.WriteBytes(reg.Path, bytes);
                AppendJournal(key, bytes);
            }
            catch (Exception ex)
            {
                _logger.StateWriteFailed(key, ex.Message);
                lock (_gate)
                    _dirty.Add(key);
            }
        }

        lock (_gate)
        {
            if (_dirty.Count > 0 && !_disposed)
                _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    public T? Load<T>(string path, string fileKey, Func<byte[], T?> parse) where T : class
    {
        var candidates = new List<string>();
        if (File.Exists(path))
            candidates.Add(path);
        if (File.Exists(path + ".bak"))
            candidates.Add(path + ".bak");

        var safe = Sanitize(fileKey);
        if (Directory.Exists(_journalDir))
        {
            var journals = Directory.GetFiles(_journalDir, safe + ".*.json");
            Array.Sort(journals, static (a, b) => string.CompareOrdinal(b, a));
            candidates.AddRange(journals);
        }

        foreach (var candidate in candidates)
        {
            try
            {
                var value = parse(File.ReadAllBytes(candidate));
                if (value is null)
                    continue;
                if (!string.Equals(candidate, path, StringComparison.Ordinal))
                    _logger.StateRecoveredFromFallback(fileKey, candidate);
                return value;
            }
            catch (Exception ex)
            {
                _logger.StateCandidateUnreadable(fileKey, candidate, ex.Message);
            }
        }

        return null;
    }

    private void AppendJournal(string fileKey, byte[] bytes)
    {
        Directory.CreateDirectory(_journalDir);
        var safe = Sanitize(fileKey);
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var path = Path.Combine(_journalDir, $"{safe}.{stamp}.json");
        File.WriteAllBytes(path, bytes);
        PruneJournal(safe);
    }

    private void PruneJournal(string safeKey)
    {
        var entries = Directory.GetFiles(_journalDir, safeKey + ".*.json");
        if (entries.Length <= _journalKeep)
            return;
        Array.Sort(entries, static (a, b) => string.CompareOrdinal(b, a));
        for (int i = _journalKeep; i < entries.Length; i++)
        {
            try { File.Delete(entries[i]); }
            catch (Exception ex) { _logger.JournalPruneFailed(entries[i], ex.Message); }
        }
    }

    private static string Sanitize(string key)
    {
        var chars = key.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            var ok = c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';
            if (!ok)
                chars[i] = '_';
        }
        return new string(chars);
    }

    private void SafeFlush()
    {
        try { Flush(); }
        catch (Exception ex) { _logger.DebouncedFlushFailed(ex.Message); }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }
        _timer.Dispose();
        Flush();
    }
}
