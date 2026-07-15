using Cove.Engine.Browser;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BrowserNookManagerTests
{
    [Fact]
    public void Open_NewNook_RecordsUrl()
    {
        var mgr = new BrowserNookManager();
        var nook = mgr.Open("p1", "https://example.com");
        Assert.Equal("https://example.com", nook.CurrentUrl);
        Assert.Single(nook.History);
    }

    [Fact]
    public void Navigate_ExistingNook_UpdatesUrl()
    {
        var mgr = new BrowserNookManager();
        mgr.Open("p1", "https://example.com");
        var nook = mgr.Navigate("p1", "https://other.com");
        Assert.Equal("https://other.com", nook!.CurrentUrl);
        Assert.Equal(2, nook.History.Count);
    }

    [Fact]
    public void Navigate_UnknownNook_ReturnsNull()
    {
        var mgr = new BrowserNookManager();
        Assert.Null(mgr.Navigate("nonexistent", "https://example.com"));
    }

    [Fact]
    public void Back_PopHistory()
    {
        var mgr = new BrowserNookManager();
        mgr.Open("p1", "https://a.com");
        mgr.Navigate("p1", "https://b.com");
        var nook = mgr.Back("p1");
        Assert.Equal("https://a.com", nook!.CurrentUrl);
    }

    [Fact]
    public void Back_AtRoot_ReturnsNull()
    {
        var mgr = new BrowserNookManager();
        mgr.Open("p1", "https://a.com");
        Assert.Null(mgr.Back("p1"));
    }

    [Fact]
    public void Forward_ReNavigates()
    {
        var mgr = new BrowserNookManager();
        mgr.Open("p1", "https://a.com");
        mgr.Navigate("p1", "https://b.com");
        mgr.Back("p1");
        var nook = mgr.Forward("p1");
        Assert.Equal("https://b.com", nook!.CurrentUrl);
    }

    [Fact]
    public void Forward_AtEnd_ReturnsNull()
    {
        var mgr = new BrowserNookManager();
        mgr.Open("p1", "https://a.com");
        Assert.Null(mgr.Forward("p1"));
    }

    [Fact]
    public void Reload_ReturnsCurrentUrl()
    {
        var mgr = new BrowserNookManager();
        mgr.Open("p1", "https://a.com");
        var url = mgr.Reload("p1");
        Assert.Equal("https://a.com", url);
    }

    [Fact]
    public void Close_RemovesNook()
    {
        var mgr = new BrowserNookManager();
        mgr.Open("p1", "https://a.com");
        mgr.Close("p1");
        Assert.Null(mgr.Get("p1"));
    }

    [Fact]
    public void Get_ReturnsNook()
    {
        var mgr = new BrowserNookManager();
        mgr.Open("p1", "https://a.com");
        var nook = mgr.Get("p1");
        Assert.NotNull(nook);
        Assert.Equal("https://a.com", nook!.CurrentUrl);
    }

    [Fact]
    public void Open_ExistingNook_RetainsCurrentPage()
    {
        var mgr = new BrowserNookManager();
        mgr.Open("p1", "https://a.com");
        mgr.Navigate("p1", "https://b.com");

        var reopened = mgr.Open("p1", "about:blank");

        Assert.Equal("https://b.com", reopened.CurrentUrl);
        Assert.Equal(2, reopened.History.Count);
        Assert.Equal(1, reopened.HistoryIndex);
    }

    [Fact]
    public void Navigate_CurrentPage_DoesNotDuplicateHistory()
    {
        var mgr = new BrowserNookManager();
        mgr.Open("p1", "https://a.com");

        var unchanged = mgr.Navigate("p1", "https://a.com");

        Assert.NotNull(unchanged);
        Assert.Single(unchanged.History);
        Assert.Equal(0, unchanged.HistoryIndex);
    }
}
