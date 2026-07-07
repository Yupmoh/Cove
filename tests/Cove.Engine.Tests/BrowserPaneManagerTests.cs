using Cove.Engine.Browser;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BrowserPaneManagerTests
{
    [Fact]
    public void Open_NewPane_RecordsUrl()
    {
        var mgr = new BrowserPaneManager();
        var pane = mgr.Open("p1", "https://example.com");
        Assert.Equal("https://example.com", pane.CurrentUrl);
        Assert.Single(pane.History);
    }

    [Fact]
    public void Navigate_ExistingPane_UpdatesUrl()
    {
        var mgr = new BrowserPaneManager();
        mgr.Open("p1", "https://example.com");
        var pane = mgr.Navigate("p1", "https://other.com");
        Assert.Equal("https://other.com", pane!.CurrentUrl);
        Assert.Equal(2, pane.History.Count);
    }

    [Fact]
    public void Navigate_UnknownPane_ReturnsNull()
    {
        var mgr = new BrowserPaneManager();
        Assert.Null(mgr.Navigate("nonexistent", "https://example.com"));
    }

    [Fact]
    public void Back_PopHistory()
    {
        var mgr = new BrowserPaneManager();
        mgr.Open("p1", "https://a.com");
        mgr.Navigate("p1", "https://b.com");
        var pane = mgr.Back("p1");
        Assert.Equal("https://a.com", pane!.CurrentUrl);
    }

    [Fact]
    public void Back_AtRoot_ReturnsNull()
    {
        var mgr = new BrowserPaneManager();
        mgr.Open("p1", "https://a.com");
        Assert.Null(mgr.Back("p1"));
    }

    [Fact]
    public void Forward_ReNavigates()
    {
        var mgr = new BrowserPaneManager();
        mgr.Open("p1", "https://a.com");
        mgr.Navigate("p1", "https://b.com");
        mgr.Back("p1");
        var pane = mgr.Forward("p1");
        Assert.Equal("https://b.com", pane!.CurrentUrl);
    }

    [Fact]
    public void Forward_AtEnd_ReturnsNull()
    {
        var mgr = new BrowserPaneManager();
        mgr.Open("p1", "https://a.com");
        Assert.Null(mgr.Forward("p1"));
    }

    [Fact]
    public void Reload_ReturnsCurrentUrl()
    {
        var mgr = new BrowserPaneManager();
        mgr.Open("p1", "https://a.com");
        var url = mgr.Reload("p1");
        Assert.Equal("https://a.com", url);
    }

    [Fact]
    public void Close_RemovesPane()
    {
        var mgr = new BrowserPaneManager();
        mgr.Open("p1", "https://a.com");
        mgr.Close("p1");
        Assert.Null(mgr.Get("p1"));
    }

    [Fact]
    public void Get_ReturnsPane()
    {
        var mgr = new BrowserPaneManager();
        mgr.Open("p1", "https://a.com");
        var pane = mgr.Get("p1");
        Assert.NotNull(pane);
        Assert.Equal("https://a.com", pane!.CurrentUrl);
    }
}
