using Cove.Engine.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ScopeGateTests
{
    [Fact]
    public void CheckAccess_AllScope_AlwaysPermitted()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess("nook-a", "ws1", "shore1", "nook-b", "ws2", "shore2", McpScope.All);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void CheckAccess_SameTab_SameNook_Permitted()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess("nook-a", "ws1", "shore1", "nook-a", "ws1", "shore1", McpScope.SameTab);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void CheckAccess_SameTab_DifferentNook_Denied()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess("nook-a", "ws1", "shore1", "nook-b", "ws1", "shore1", McpScope.SameTab);
        Assert.False(result.Allowed);
        Assert.Equal("access_denied", result.ErrorCode);
    }

    [Fact]
    public void CheckAccess_SameBay_SameBay_Permitted()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess("nook-a", "ws1", "shore1", "nook-b", "ws1", "shore2", McpScope.SameBay);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void CheckAccess_SameBay_DifferentBay_Denied()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess("nook-a", "ws1", "shore1", "nook-b", "ws2", "shore1", McpScope.SameBay);
        Assert.False(result.Allowed);
        Assert.Equal("access_denied", result.ErrorCode);
    }

    [Fact]
    public void CheckAccess_NoCallerNook_DefaultPermitsReads()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess(null, null, null, "nook-b", "ws2", "shore2", McpScope.All);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void CheckAccess_UnknownTargetNook_DeniedWithNotFound()
    {
        var gate = new ScopeGate();
        var result = gate.CheckAccess("nook-a", "ws1", "shore1", null, null, null, McpScope.SameTab);
        Assert.False(result.Allowed);
        Assert.Equal("not_found", result.ErrorCode);
    }
}
