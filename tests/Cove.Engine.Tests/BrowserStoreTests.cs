using Cove.Engine.Browser;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BrowserStoreTests
{
    private static string NewDir()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-browser-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void RecordVisit_IncrementsCount()
    {
        var store = new BrowserStore(NewDir(), NullLogger.Instance);
        store.RecordVisit("https://example.com", "Example");
        store.RecordVisit("https://example.com", "Example");

        var top = store.GetTopVisited();
        Assert.Single(top);
        Assert.Equal(2, top[0].VisitCount);
    }

    [Fact]
    public void SearchHistory_FindsByUrl()
    {
        var store = new BrowserStore(NewDir(), NullLogger.Instance);
        store.RecordVisit("https://github.com/cove", "Cove");
        store.RecordVisit("https://example.com", "Example");

        var results = store.SearchHistory("github");
        Assert.Single(results);
        Assert.Contains("github", results[0].Url);
    }

    [Fact]
    public void SearchHistory_FindsByTitle()
    {
        var store = new BrowserStore(NewDir(), NullLogger.Instance);
        store.RecordVisit("https://example.com", "Cove Documentation");

        var results = store.SearchHistory("cove");
        Assert.Single(results);
        Assert.Contains("Cove", results[0].Title);
    }

    [Fact]
    public void GetTopVisited_OrdersByCount()
    {
        var store = new BrowserStore(NewDir(), NullLogger.Instance);
        store.RecordVisit("https://a.com", "A");
        store.RecordVisit("https://b.com", "B");
        store.RecordVisit("https://b.com", "B");

        var top = store.GetTopVisited();
        Assert.Equal("B", top[0].Title);
        Assert.Equal(2, top[0].VisitCount);
    }

    [Fact]
    public void AddAnnotation_Persists()
    {
        var store = new BrowserStore(NewDir(), NullLogger.Instance);
        var ann = store.AddAnnotation("https://example.com", "element", "{}", "Fix this button", "alice");

        Assert.False(string.IsNullOrEmpty(ann.Id));
        Assert.Equal("https://example.com", ann.UrlKey);
        Assert.False(ann.Resolved);
    }

    [Fact]
    public void ListAnnotations_FiltersByUrlKey()
    {
        var store = new BrowserStore(NewDir(), NullLogger.Instance);
        store.AddAnnotation("https://a.com", "element", "{}", "comment A", "alice");
        store.AddAnnotation("https://b.com", "element", "{}", "comment B", "bob");

        var annotations = store.ListAnnotations(urlKey: "https://a.com");
        Assert.Single(annotations);
        Assert.Equal("comment A", annotations[0].Text);
    }

    [Fact]
    public void ListAnnotations_FiltersUnresolved()
    {
        var store = new BrowserStore(NewDir(), NullLogger.Instance);
        var ann1 = store.AddAnnotation("https://a.com", "element", "{}", "unresolved", "alice");
        var ann2 = store.AddAnnotation("https://a.com", "element", "{}", "will resolve", "bob");
        store.ResolveAnnotation(ann2.Id);

        var unresolved = store.ListAnnotations(urlKey: "https://a.com", unresolved: true);
        Assert.Single(unresolved);
        Assert.Equal("unresolved", unresolved[0].Text);
    }

    [Fact]
    public void ResolveAnnotation_MarksResolved()
    {
        var store = new BrowserStore(NewDir(), NullLogger.Instance);
        var ann = store.AddAnnotation("https://a.com", "element", "{}", "comment", "alice");

        Assert.True(store.ResolveAnnotation(ann.Id));
        var all = store.ListAnnotations();
        Assert.True(all[0].Resolved);
    }

    [Fact]
    public void DeleteAnnotation_RemovesAnnotation()
    {
        var store = new BrowserStore(NewDir(), NullLogger.Instance);
        var ann = store.AddAnnotation("https://a.com", "element", "{}", "comment", "alice");

        Assert.True(store.DeleteAnnotation(ann.Id));
        Assert.Empty(store.ListAnnotations());
    }

    [Fact]
    public void NormalizeUrlKey_StripsFragment()
    {
        var key = BrowserStore.NormalizeUrlKey("https://example.com/page?q=1#section");
        Assert.DoesNotContain("#section", key);
    }

    [Fact]
    public void NormalizeUrlKey_PreservesQuery()
    {
        var key = BrowserStore.NormalizeUrlKey("https://example.com/page?q=1");
        Assert.Contains("?q=1", key);
    }
    [Fact]
    public void SetPaneState_PersistsAndRetrieves()
    {
        var store = new BrowserPaneStateStore(NewDir(), NullLogger.Instance);
        var state = new BrowserPaneState(
            "pane-1", "https://example.com", "Example", "favicon.ico",
            new[] { "https://a.com" }, new[] { "https://b.com" },
            new System.Collections.Generic.Dictionary<string, double> { ["example.com"] = 1.5 },
            false, "engine-1", System.DateTimeOffset.UtcNow);
        store.Save("pane-1", state);

        var loaded = store.Load("pane-1");
        Assert.NotNull(loaded);
        Assert.Equal("https://example.com", loaded!.Url);
        Assert.Equal("Example", loaded.Title);
        Assert.Equal("favicon.ico", loaded.Favicon);
        Assert.Single(loaded.BackStack);
        Assert.Single(loaded.ForwardStack);
        Assert.Equal(1.5, loaded.PerSiteZoom["example.com"]);
        Assert.False(loaded.Incognito);
        Assert.Equal("engine-1", loaded.EngineTargetId);
    }

    [Fact]
    public void SetPaneState_OverwritesPrevious()
    {
        var store = new BrowserPaneStateStore(NewDir(), NullLogger.Instance);
        var state1 = new BrowserPaneState("pane-1", "https://a.com", "A", "", [], [], new(), false, "e1", System.DateTimeOffset.UtcNow);
        store.Save("pane-1", state1);
        var state2 = new BrowserPaneState("pane-1", "https://b.com", "B", "", [], [], new(), true, "e2", System.DateTimeOffset.UtcNow);
        store.Save("pane-1", state2);

        var loaded = store.Load("pane-1");
        Assert.NotNull(loaded);
        Assert.Equal("https://b.com", loaded!.Url);
        Assert.Equal("B", loaded.Title);
        Assert.True(loaded.Incognito);
    }

    [Fact]
    public void GetPaneState_Nonexistent_ReturnsNull()
    {
        var store = new BrowserPaneStateStore(NewDir(), NullLogger.Instance);
        Assert.Null(store.Load("nonexistent"));
    }

    [Fact]
    public void DeletePaneState_RemovesState()
    {
        var store = new BrowserPaneStateStore(NewDir(), NullLogger.Instance);
        var state = new BrowserPaneState("pane-1", "https://a.com", "A", "", [], [], new(), false, "e1", System.DateTimeOffset.UtcNow);
        store.Save("pane-1", state);

        Assert.True(store.Delete("pane-1"));
        Assert.Null(store.Load("pane-1"));
    }

    [Fact]
    public void ExportHistoryToJsonFile_WritesAtriumShape()
    {
        var dir = NewDir();
        var store = new BrowserStore(dir, NullLogger.Instance);
        store.RecordVisit("https://github.com/cove", "Cove");
        store.RecordVisit("https://example.com", "Example");

        var exportPath = store.ExportHistoryToJsonFile();
        Assert.True(System.IO.File.Exists(exportPath));
        var json = System.IO.File.ReadAllText(exportPath);
        Assert.Contains("\"url\":", json);
        Assert.Contains("\"title\":", json);
        Assert.Contains("\"visit_count\":", json);
        Assert.Contains("\"last_visited\":", json);
        Assert.Contains("github.com", json);
        Assert.Contains("example.com", json);
    }

    [Fact]
    public void ExportHistoryToJsonFile_EmptyHistory_ProducesEmptyArray()
    {
        var dir = NewDir();
        var store = new BrowserStore(dir, NullLogger.Instance);
        var exportPath = store.ExportHistoryToJsonFile();
        var json = System.IO.File.ReadAllText(exportPath);
        Assert.Equal("[]", json.Trim());
    }

    [Fact]
    public void ExportHistoryToJsonFile_IncludesVisitCountAndTimestamp()
    {
        var dir = NewDir();
        var store = new BrowserStore(dir, NullLogger.Instance);
        store.RecordVisit("https://a.com", "A");
        store.RecordVisit("https://a.com", "A");

        var exportPath = store.ExportHistoryToJsonFile();
        var json = System.IO.File.ReadAllText(exportPath);
        Assert.Contains("\"visit_count\": 2", json);
        Assert.Contains("\"last_visited\":", json);
    }

    [Fact]
    public void ExportHistoryToJsonFile_PathIsBrowserHistoryJson()
    {
        var dir = NewDir();
        var store = new BrowserStore(dir, NullLogger.Instance);
        var exportPath = store.ExportHistoryToJsonFile();
        Assert.EndsWith("browser-history.json", exportPath);
    }
    [Fact]
    public void PaneState_RoundTripsForResumeOnRestart()
    {
        var dir = NewDir();
        var store1 = new BrowserPaneStateStore(dir, NullLogger.Instance);
        var original = new BrowserPaneState(
            "pane-resume", "https://example.com/page", "Example Page", "fav.ico",
            new[] { "https://example.com/prev1", "https://example.com/prev2" },
            new[] { "https://example.com/next1" },
            new System.Collections.Generic.Dictionary<string, double> { ["example.com"] = 1.25 },
            false, "target-1", System.DateTimeOffset.UtcNow);

        store1.Save("pane-resume", original);

        var store2 = new BrowserPaneStateStore(dir, NullLogger.Instance);
        var restored = store2.Load("pane-resume");

        Assert.NotNull(restored);
        Assert.Equal("https://example.com/page", restored!.Url);
        Assert.Equal(2, restored.BackStack.Length);
        Assert.Equal("https://example.com/prev1", restored.BackStack[0]);
        Assert.Single(restored.ForwardStack);
        Assert.Equal(1.25, restored.PerSiteZoom["example.com"]);
    }
}
