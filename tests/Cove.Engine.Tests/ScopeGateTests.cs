using Cove.Engine.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ScopeGateTests
{
    [Fact]
    public void CheckAccess_AllScope_AlwaysPermitted()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess("pane-a", "ws1", "room1", "pane-b", "ws2", "room2", McpScope.All);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void CheckAccess_SameTab_SamePane_Permitted()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess("pane-a", "ws1", "room1", "pane-a", "ws1", "room1", McpScope.SameTab);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void CheckAccess_SameTab_DifferentPane_Denied()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess("pane-a", "ws1", "room1", "pane-b", "ws1", "room1", McpScope.SameTab);
        Assert.False(result.Allowed);
        Assert.Equal("access_denied", result.ErrorCode);
    }

    [Fact]
    public void CheckAccess_SameWorkspace_SameWorkspace_Permitted()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess("pane-a", "ws1", "room1", "pane-b", "ws1", "room2", McpScope.SameWorkspace);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void CheckAccess_SameWorkspace_DifferentWorkspace_Denied()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess("pane-a", "ws1", "room1", "pane-b", "ws2", "room1", McpScope.SameWorkspace);
        Assert.False(result.Allowed);
        Assert.Equal("access_denied", result.ErrorCode);
    }

    [Fact]
    public void CheckAccess_NoCallerPane_DefaultPermitsReads()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess(null, null, null, "pane-b", "ws2", "room2", McpScope.All);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void CheckAccess_UnknownTargetPane_DeniedWithNotFound()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess("pane-a", "ws1", "room1", null, null, null, McpScope.SameTab);
        Assert.False(result.Allowed);
        Assert.Equal("not_found", result.ErrorCode);
    }
}
