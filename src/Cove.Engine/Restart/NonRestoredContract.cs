namespace Cove.Engine.Restart;

public enum NonRestoredKind
{
    UnsavedEditorBuffers,
    InFlightForegroundCommands,
    BrowserCookiesAcrossUpgrade,
    ReapedAdapterSessions,
    AdapterSessionResume,
}

public static class NonRestoredContract
{
    public static IReadOnlyDictionary<NonRestoredKind, string> Items { get; } = new Dictionary<NonRestoredKind, string>
    {
        [NonRestoredKind.UnsavedEditorBuffers] = "Unsaved editor buffers are not restored — flushed to disk on close, no separate journal.",
        [NonRestoredKind.InFlightForegroundCommands] = "In-flight HTTP/long-running foreground non-adapter commands are not restored — killed on process death, last command kept for Replay.",
        [NonRestoredKind.BrowserCookiesAcrossUpgrade] = "Browser cookies across a webview-engine upgrade are not restored — not portable.",
        [NonRestoredKind.ReapedAdapterSessions] = "Reaped adapter sessions are not restored — fresh-launched with overrides reapplied (WS-78).",
    };
}
