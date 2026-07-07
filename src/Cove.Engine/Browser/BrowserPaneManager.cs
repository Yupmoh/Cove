namespace Cove.Engine.Browser;

public sealed record BrowserPane(
    string PaneId,
    string CurrentUrl,
    IReadOnlyList<string> History,
    int HistoryIndex)
{
    public bool CanGoBack => HistoryIndex > 0;
    public bool CanGoForward => HistoryIndex < History.Count - 1;
}

public sealed class BrowserPaneManager
{
    private readonly Dictionary<string, BrowserPane> _panes = new();

    public BrowserPane Open(string paneId, string url)
    {
        var pane = new BrowserPane(paneId, url, new List<string> { url }, 0);
        _panes[paneId] = pane;
        return pane;
    }

    public BrowserPane? Navigate(string paneId, string url)
    {
        if (!_panes.TryGetValue(paneId, out var existing))
            return null;
        var history = existing.History.Take(existing.HistoryIndex + 1).Append(url).ToList();
        var pane = existing with { CurrentUrl = url, History = history, HistoryIndex = history.Count - 1 };
        _panes[paneId] = pane;
        return pane;
    }

    public BrowserPane? Back(string paneId)
    {
        if (!_panes.TryGetValue(paneId, out var existing) || !existing.CanGoBack)
            return null;
        var newIndex = existing.HistoryIndex - 1;
        var pane = existing with { HistoryIndex = newIndex, CurrentUrl = existing.History[newIndex] };
        _panes[paneId] = pane;
        return pane;
    }

    public BrowserPane? Forward(string paneId)
    {
        if (!_panes.TryGetValue(paneId, out var existing) || !existing.CanGoForward)
            return null;
        var newIndex = existing.HistoryIndex + 1;
        var pane = existing with { HistoryIndex = newIndex, CurrentUrl = existing.History[newIndex] };
        _panes[paneId] = pane;
        return pane;
    }

    public string? Reload(string paneId)
    {
        if (!_panes.TryGetValue(paneId, out var existing))
            return null;
        return existing.CurrentUrl;
    }

    public BrowserPane? Get(string paneId) =>
        _panes.TryGetValue(paneId, out var pane) ? pane : null;

    public void Close(string paneId) => _panes.Remove(paneId);
}
