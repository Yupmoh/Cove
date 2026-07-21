using Cove.Protocol;

namespace Cove.Engine.Browser;

public sealed record BrowserNook(
    string NookId,
    string CurrentUrl,
    IReadOnlyList<string> History,
    int HistoryIndex)
{
    public bool CanGoBack => HistoryIndex > 0;
    public bool CanGoForward => HistoryIndex < History.Count - 1;
}

public sealed class BrowserNookManager
{
    private readonly object _sync = new();
    private readonly Dictionary<string, BrowserNook> _nooks = new();

    public BrowserNook Open(string nookId, string url)
    {
        lock (_sync)
        {
            if (_nooks.TryGetValue(nookId, out var existing))
                return existing;
            var nook = new BrowserNook(nookId, url, [url], 0);
            _nooks[nookId] = nook;
            return nook;
        }
    }

    public BrowserNook? Navigate(string nookId, string url)
    {
        lock (_sync)
        {
            if (!_nooks.TryGetValue(nookId, out var existing))
                return null;
            if (existing.CurrentUrl == url)
                return existing;
            var history = existing.History
                .Take(existing.HistoryIndex + 1)
                .Append(url)
                .ToArray();
            var nook = existing with
            {
                CurrentUrl = url,
                History = history,
                HistoryIndex = history.Length - 1,
            };
            _nooks[nookId] = nook;
            return nook;
        }
    }

    public BrowserNook? Back(string nookId)
    {
        lock (_sync)
        {
            if (!_nooks.TryGetValue(nookId, out var existing)
                || !existing.CanGoBack)
            {
                return null;
            }
            var newIndex = existing.HistoryIndex - 1;
            var nook = existing with
            {
                HistoryIndex = newIndex,
                CurrentUrl = existing.History[newIndex],
            };
            _nooks[nookId] = nook;
            return nook;
        }
    }

    public BrowserNook? Forward(string nookId)
    {
        lock (_sync)
        {
            if (!_nooks.TryGetValue(nookId, out var existing)
                || !existing.CanGoForward)
            {
                return null;
            }
            var newIndex = existing.HistoryIndex + 1;
            var nook = existing with
            {
                HistoryIndex = newIndex,
                CurrentUrl = existing.History[newIndex],
            };
            _nooks[nookId] = nook;
            return nook;
        }
    }

    public string? Reload(string nookId)
    {
        lock (_sync)
        {
            return _nooks.TryGetValue(nookId, out var existing)
                ? existing.CurrentUrl
                : null;
        }
    }

    public BrowserNook? Get(string nookId)
    {
        lock (_sync)
            return _nooks.TryGetValue(nookId, out var nook) ? nook : null;
    }

    public void Close(string nookId)
    {
        lock (_sync)
            _nooks.Remove(nookId);
    }

    public HandoffBrowserNookDto[] Snapshot()
    {
        lock (_sync)
        {
            var snapshot = new HandoffBrowserNookDto[_nooks.Count];
            var index = 0;
            foreach (var nook in _nooks.Values)
            {
                snapshot[index++] = new HandoffBrowserNookDto(
                    nook.NookId,
                    nook.CurrentUrl,
                    nook.History.ToArray(),
                    nook.HistoryIndex);
            }
            return snapshot;
        }
    }

    public void Restore(
        IReadOnlyList<HandoffBrowserNookDto>? snapshot)
    {
        if (snapshot is null || snapshot.Count == 0)
            return;
        var restored = new BrowserNook[snapshot.Count];
        var nookIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < snapshot.Count; index++)
        {
            var item = snapshot[index];
            if (string.IsNullOrWhiteSpace(item.NookId)
                || string.IsNullOrWhiteSpace(item.CurrentUrl)
                || item.History is null
                || item.History.Length == 0
                || item.HistoryIndex < 0
                || item.HistoryIndex >= item.History.Length
                || !string.Equals(
                    item.CurrentUrl,
                    item.History[item.HistoryIndex],
                    StringComparison.Ordinal)
                || !nookIds.Add(item.NookId))
            {
                throw new ArgumentException(
                    "browser handoff state is invalid",
                    nameof(snapshot));
            }
            restored[index] = new BrowserNook(
                item.NookId,
                item.CurrentUrl,
                item.History.ToArray(),
                item.HistoryIndex);
        }
        lock (_sync)
        {
            foreach (var nook in restored)
                _nooks[nook.NookId] = nook;
        }
    }
}
