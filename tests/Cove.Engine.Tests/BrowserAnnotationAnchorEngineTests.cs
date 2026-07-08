using Cove.Engine.Browser;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BrowserAnnotationAnchorEngineTests
{
    [Fact]
    public void SerializeDeserialize_RoundTrips()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#header", 5, "Hello World", 100, 50);
        var json = engine.SerializeAnchor(anchor);
        var restored = engine.DeserializeAnchor(json);
        Assert.NotNull(restored);
        Assert.Equal("#header", restored!.Selector);
        Assert.Equal(5, restored.TextOffset);
        Assert.Equal("Hello World", restored.SnapshotText);
        Assert.Equal(100, restored.ViewportTop);
        Assert.Equal(50, restored.ViewportLeft);
    }

    [Fact]
    public void DeserializeAnchor_NullOrEmpty_ReturnsNull()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        Assert.Null(engine.DeserializeAnchor(null));
        Assert.Null(engine.DeserializeAnchor(""));
        Assert.Null(engine.DeserializeAnchor("   "));
    }

    [Fact]
    public void DeserializeAnchor_InvalidJson_ReturnsNull()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        Assert.Null(engine.DeserializeAnchor("{invalid json}"));
    }

    [Fact]
    public void ReAnchor_UpdatesViewportPosition()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 100, 50);
        var reAnchored = engine.ReAnchor(anchor, 200, 75);
        Assert.NotNull(reAnchored);
        Assert.Equal(200, reAnchored!.ViewportTop);
        Assert.Equal(75, reAnchored.ViewportLeft);
    }

    [Fact]
    public void ReAnchor_NoChange_ReturnsSameAnchor()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 100, 50);
        var result = engine.ReAnchor(anchor, 100, 50);
        Assert.Same(anchor, result);
    }

    [Fact]
    public void ReAnchor_NullInput_ReturnsNull()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        Assert.Null(engine.ReAnchor(null, 100, 50));
    }

    [Fact]
    public void IsAnchorVisible_WithinViewport_ReturnsTrue()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 50, 30);
        Assert.True(engine.IsAnchorVisible(anchor, 100, 200));
    }

    [Fact]
    public void IsAnchorVisible_BelowViewport_ReturnsFalse()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 150, 30);
        Assert.False(engine.IsAnchorVisible(anchor, 100, 200));
    }

    [Fact]
    public void IsAnchorVisible_RightOfViewport_ReturnsFalse()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 50, 250);
        Assert.False(engine.IsAnchorVisible(anchor, 100, 200));
    }

    [Fact]
    public void IsAnchorVisible_InvalidViewport_ReturnsFalse()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 50, 30);
        Assert.False(engine.IsAnchorVisible(anchor, 0, 0));
        Assert.False(engine.IsAnchorVisible(anchor, -1, 100));
    }

    [Fact]
    public void DistanceFromViewport_Above_ReturnsPositiveDistance()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 50, 0);
        var dist = engine.DistanceFromViewport(anchor, 100, 500);
        Assert.Equal(50, dist);
    }

    [Fact]
    public void DistanceFromViewport_Below_ReturnsPositiveDistance()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 700, 0);
        var dist = engine.DistanceFromViewport(anchor, 100, 500);
        Assert.Equal(100, dist);
    }

    [Fact]
    public void DistanceFromViewport_WithinViewport_ReturnsZero()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 300, 0);
        var dist = engine.DistanceFromViewport(anchor, 100, 500);
        Assert.Equal(0, dist);
    }
}
