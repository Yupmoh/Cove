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
    private readonly Dictionary<string, BrowserNook> _nooks = new();

    public BrowserNook Open(string nookId, string url)
    {
        if (_nooks.TryGetValue(nookId, out var existing))
            return existing;
        var nook = new BrowserNook(nookId, url, new List<string> { url }, 0);
        _nooks[nookId] = nook;
        return nook;
    }

    public BrowserNook? Navigate(string nookId, string url)
    {
        if (!_nooks.TryGetValue(nookId, out var existing))
            return null;
        if (existing.CurrentUrl == url)
            return existing;
        var history = existing.History.Take(existing.HistoryIndex + 1).Append(url).ToList();
        var nook = existing with { CurrentUrl = url, History = history, HistoryIndex = history.Count - 1 };
        _nooks[nookId] = nook;
        return nook;
    }

    public BrowserNook? Back(string nookId)
    {
        if (!_nooks.TryGetValue(nookId, out var existing) || !existing.CanGoBack)
            return null;
        var newIndex = existing.HistoryIndex - 1;
        var nook = existing with { HistoryIndex = newIndex, CurrentUrl = existing.History[newIndex] };
        _nooks[nookId] = nook;
        return nook;
    }

    public BrowserNook? Forward(string nookId)
    {
        if (!_nooks.TryGetValue(nookId, out var existing) || !existing.CanGoForward)
            return null;
        var newIndex = existing.HistoryIndex + 1;
        var nook = existing with { HistoryIndex = newIndex, CurrentUrl = existing.History[newIndex] };
        _nooks[nookId] = nook;
        return nook;
    }

    public string? Reload(string nookId)
    {
        if (!_nooks.TryGetValue(nookId, out var existing))
            return null;
        return existing.CurrentUrl;
    }

    public BrowserNook? Get(string nookId) =>
        _nooks.TryGetValue(nookId, out var nook) ? nook : null;

    public void Close(string nookId) => _nooks.Remove(nookId);
}
