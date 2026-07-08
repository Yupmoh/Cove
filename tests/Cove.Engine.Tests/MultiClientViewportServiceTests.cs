using Cove.Engine.Tui;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class MultiClientViewportServiceTests
{
    [Fact]
    public void AttachClient_AddsToPane()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        svc.AttachClient(new ClientViewport("c1", 24, 80, 1));
        Assert.Equal(1, svc.GetClientCount(1));
        Assert.Contains("c1", svc.GetClientsForPane(1));
    }

    [Fact]
    public void DetachClient_RemovesFromPane()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        svc.AttachClient(new ClientViewport("c1", 24, 80, 1));
        Assert.True(svc.DetachClient("c1"));
        Assert.Equal(0, svc.GetClientCount(1));
    }

    [Fact]
    public void DetachClient_NotAttached_ReturnsFalse()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        Assert.False(svc.DetachClient("nonexistent"));
    }

    [Fact]
    public void ReconcilePtySize_SingleClient_ReturnsClientSize()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        svc.AttachClient(new ClientViewport("c1", 24, 80, 1));
        var size = svc.ReconcilePtySize(1);
        Assert.NotNull(size);
        Assert.Equal((24, 80), size);
    }

    [Fact]
    public void ReconcilePtySize_MultipleClients_ReturnsMinimum()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        svc.AttachClient(new ClientViewport("c1", 30, 100, 1));
        svc.AttachClient(new ClientViewport("c2", 24, 80, 1));
        svc.AttachClient(new ClientViewport("c3", 50, 120, 1));
        var size = svc.ReconcilePtySize(1);
        Assert.NotNull(size);
        Assert.Equal((24, 80), size);
    }

    [Fact]
    public void ReconcilePtySize_NoClients_ReturnsNull()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        Assert.Null(svc.ReconcilePtySize(1));
    }

    [Fact]
    public void ReconcilePtySize_AfterDetach_Recalculates()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        svc.AttachClient(new ClientViewport("c1", 30, 100, 1));
        svc.AttachClient(new ClientViewport("c2", 24, 80, 1));
        svc.DetachClient("c2");
        var size = svc.ReconcilePtySize(1);
        Assert.Equal((30, 100), size);
    }

    [Fact]
    public void UpdateViewportSize_ChangesDimensions()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        svc.AttachClient(new ClientViewport("c1", 24, 80, 1));
        Assert.True(svc.UpdateViewportSize("c1", 50, 200));
        var size = svc.ReconcilePtySize(1);
        Assert.Equal((50, 200), size);
    }

    [Fact]
    public void UpdateViewportSize_NotAttached_ReturnsFalse()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        Assert.False(svc.UpdateViewportSize("nonexistent", 50, 200));
    }

    [Fact]
    public void UpdateViewportSize_InvalidDimensions_ReturnsFalse()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        svc.AttachClient(new ClientViewport("c1", 24, 80, 1));
        Assert.False(svc.UpdateViewportSize("c1", 0, 80));
        Assert.False(svc.UpdateViewportSize("c1", 24, -1));
    }

    [Fact]
    public void AttachClient_InvalidDimensions_Throws()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        Assert.Throws<ArgumentException>(() => svc.AttachClient(new ClientViewport("c1", 0, 80, 1)));
        Assert.Throws<ArgumentException>(() => svc.AttachClient(new ClientViewport("c1", 24, 0, 1)));
    }

    [Fact]
    public void AttachClient_EmptyClientId_Throws()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        Assert.Throws<ArgumentException>(() => svc.AttachClient(new ClientViewport("", 24, 80, 1)));
    }

    [Fact]
    public void MultiplePanes_TrackedSeparately()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        svc.AttachClient(new ClientViewport("c1", 24, 80, 1));
        svc.AttachClient(new ClientViewport("c2", 30, 100, 2));
        Assert.Equal(1, svc.GetClientCount(1));
        Assert.Equal(1, svc.GetClientCount(2));
        Assert.Equal((24, 80), svc.ReconcilePtySize(1));
        Assert.Equal((30, 100), svc.ReconcilePtySize(2));
    }

    [Fact]
    public void ReattachClient_AfterDetach_Works()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        svc.AttachClient(new ClientViewport("c1", 24, 80, 1));
        svc.DetachClient("c1");
        svc.AttachClient(new ClientViewport("c1", 30, 100, 1));
        Assert.Equal(1, svc.GetClientCount(1));
        Assert.Equal((30, 100), svc.ReconcilePtySize(1));
    }

    [Fact]
    public void ReattachClient_ToDifferentPane_MovesClient()
    {
        var svc = new MultiClientViewportService(NullLogger.Instance);
        svc.AttachClient(new ClientViewport("c1", 24, 80, 1));
        svc.AttachClient(new ClientViewport("c1", 24, 80, 2));
        Assert.Equal(0, svc.GetClientCount(1));
        Assert.Equal(1, svc.GetClientCount(2));
    }
}
