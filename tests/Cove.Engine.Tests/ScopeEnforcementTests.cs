using System.Text.Json;
using Cove.Engine.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ScopeEnforcementTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-scope-" + Guid.NewGuid().ToString("N"));

    private static Cove.Engine.Pty.NookRegistry NewNooks()
        => new(Cove.Platform.Pty.PtyHostFactory.Create(NullLogger.Instance), NullLogger.Instance);

    private static string SpawnNook(Cove.Engine.Pty.NookRegistry nooks)
    {
        var req = new Cove.Protocol.ControlRequest("1", "cove://commands/nook.spawn",
            JsonDocument.Parse("""{"command":"/bin/sh","args":["-c","sleep 30"]}""").RootElement.Clone());
        var resp = EngineCommandRouter.RouteAsync(req, nooks: nooks).GetAwaiter().GetResult();
        return resp!.Data!.Value.GetProperty("nookId").GetString()!;
    }

    [Fact]
    public async Task NookWrite_CrossNook_SameTabScope_ReturnsAccessDenied()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var nooks = NewNooks();
        var scopeStore = new NookScopeStore(NewDir(), NullLogger.Instance);
        string callerNook = "", targetNook = "";
        try
        {
            callerNook = SpawnNook(nooks);
            targetNook = SpawnNook(nooks);
            scopeStore.SetScope(callerNook, McpScope.SameTab);
            scopeStore.SetScope(targetNook, McpScope.All);
            var prm = JsonDocument.Parse($"{{\"nookId\":\"{targetNook}\",\"dataBase64\":\"aGk=\"}}").RootElement.Clone();
            var request = new Cove.Protocol.ControlRequest("1", "cove://commands/nook.write", prm, CallerNookId: callerNook);
            var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks, nookScopes: scopeStore);
            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("access_denied", response.Error?.Code);
        }
        finally
        {
            try { nooks.Kill(callerNook); } catch { }
            try { nooks.Kill(targetNook); } catch { }
        }
    }

    [Fact]
    public async Task NookWrite_SameNook_SameTabScope_Allowed()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var nooks = NewNooks();
        var scopeStore = new NookScopeStore(NewDir(), NullLogger.Instance);
        string nook = "";
        try
        {
            nook = SpawnNook(nooks);
            scopeStore.SetScope(nook, McpScope.SameTab);
            var prm = JsonDocument.Parse($"{{\"nookId\":\"{nook}\",\"dataBase64\":\"aGk=\"}}").RootElement.Clone();
            var request = new Cove.Protocol.ControlRequest("1", "cove://commands/nook.write", prm, CallerNookId: nook);
            var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks, nookScopes: scopeStore);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
        }
        finally { try { nooks.Kill(nook); } catch { } }
    }

    [Fact]
    public async Task NookWrite_CrossNook_AllScope_Allowed()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var nooks = NewNooks();
        var scopeStore = new NookScopeStore(NewDir(), NullLogger.Instance);
        string callerNook = "", targetNook = "";
        try
        {
            callerNook = SpawnNook(nooks);
            targetNook = SpawnNook(nooks);
            scopeStore.SetScope(callerNook, McpScope.All);
            var prm = JsonDocument.Parse($"{{\"nookId\":\"{targetNook}\",\"dataBase64\":\"aGk=\"}}").RootElement.Clone();
            var request = new Cove.Protocol.ControlRequest("1", "cove://commands/nook.write", prm, CallerNookId: callerNook);
            var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks, nookScopes: scopeStore);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
        }
        finally
        {
            try { nooks.Kill(callerNook); } catch { }
            try { nooks.Kill(targetNook); } catch { }
        }
    }

    [Fact]
    public async Task NookWrite_NoCallerNookId_Allowed()
    {
        if (System.OperatingSystem.IsWindows()) return;
        var nooks = NewNooks();
        var scopeStore = new NookScopeStore(NewDir(), NullLogger.Instance);
        string nook = "";
        try
        {
            nook = SpawnNook(nooks);
            scopeStore.SetScope(nook, McpScope.SameTab);
            var prm = JsonDocument.Parse($"{{\"nookId\":\"{nook}\",\"dataBase64\":\"aGk=\"}}").RootElement.Clone();
            var request = new Cove.Protocol.ControlRequest("1", "cove://commands/nook.write", prm);
            var response = await EngineCommandRouter.RouteAsync(request, nooks: nooks, nookScopes: scopeStore);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
        }
        finally { try { nooks.Kill(nook); } catch { } }
    }
}
