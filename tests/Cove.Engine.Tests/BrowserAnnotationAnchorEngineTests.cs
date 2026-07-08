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
        Assert.Equal(100, restored.ElementTop);
        Assert.Equal(50, restored.ElementLeft);
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
    public void ComputeViewportPosition_SubtractsScroll()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 500, 200);
        var pos = engine.ComputeViewportPosition(anchor, scrollY: 300, scrollX: 50);
        Assert.Equal(200, pos.Top);
        Assert.Equal(150, pos.Left);
    }

    [Fact]
    public void ComputeViewportPosition_ZeroScroll_ReturnsElementPosition()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 500, 200);
        var pos = engine.ComputeViewportPosition(anchor, scrollY: 0, scrollX: 0);
        Assert.Equal(500, pos.Top);
        Assert.Equal(200, pos.Left);
    }

    [Fact]
    public void IsAnchorVisible_WithinViewport_ReturnsTrue()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 500, 200);
        Assert.True(engine.IsAnchorVisible(anchor, scrollY: 400, scrollX: 100, viewportHeight: 800, viewportWidth: 600));
    }

    [Fact]
    public void IsAnchorVisible_ScrolledPast_ReturnsFalse()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 500, 200);
        Assert.False(engine.IsAnchorVisible(anchor, scrollY: 600, scrollX: 0, viewportHeight: 800, viewportWidth: 600));
    }

    [Fact]
    public void IsAnchorVisible_NotYetScrolledTo_ReturnsFalse()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 500, 200);
        Assert.False(engine.IsAnchorVisible(anchor, scrollY: 0, scrollX: 0, viewportHeight: 400, viewportWidth: 600));
    }

    [Fact]
    public void IsAnchorVisible_InvalidViewport_ReturnsFalse()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 50, 30);
        Assert.False(engine.IsAnchorVisible(anchor, 0, 0, 0, 0));
        Assert.False(engine.IsAnchorVisible(anchor, 0, 0, -1, 100));
    }

    [Fact]
    public void DistanceFromViewport_AboveVisibleArea_ReturnsPositiveDistance()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 100, 0);
        var dist = engine.DistanceFromViewport(anchor, scrollY: 200, viewportHeight: 500);
        Assert.Equal(100, dist);
    }

    [Fact]
    public void DistanceFromViewport_BelowVisibleArea_ReturnsPositiveDistance()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 800, 0);
        var dist = engine.DistanceFromViewport(anchor, scrollY: 0, viewportHeight: 500);
        Assert.Equal(300, dist);
    }

    [Fact]
    public void DistanceFromViewport_WithinViewport_ReturnsZero()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "text", 300, 0);
        var dist = engine.DistanceFromViewport(anchor, scrollY: 0, viewportHeight: 500);
        Assert.Equal(0, dist);
    }

    [Fact]
    public void VerifyAnchorText_MatchingSnapshot_ReturnsTrue()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 6, "World", 0, 0);
        Assert.True(engine.VerifyAnchorText(anchor, "Hello World Here"));
    }

    [Fact]
    public void VerifyAnchorText_NonMatchingSnapshot_ReturnsFalse()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 6, "World", 0, 0);
        Assert.False(engine.VerifyAnchorText(anchor, "Hello Earth Here"));
    }

    [Fact]
    public void VerifyAnchorText_TextTooShort_ReturnsFalse()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 10, "World", 0, 0);
        Assert.False(engine.VerifyAnchorText(anchor, "Short"));
    }

    [Fact]
    public void VerifyAnchorText_EmptySnapshot_ReturnsTrue()
    {
        var engine = new BrowserAnnotationAnchorEngine(NullLogger.Instance);
        var anchor = new BrowserAnnotationAnchor("#elem", 0, "", 0, 0);
        Assert.True(engine.VerifyAnchorText(anchor, "anything"));
    }
}
