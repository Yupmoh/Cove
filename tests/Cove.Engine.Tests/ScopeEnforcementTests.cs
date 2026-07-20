using System.Text.Json;
using Cove.Engine.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Cove.Testing;

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

    public static TheoryData<string> ScopedCommandUris => new()
    {
        "cove://commands/nook.write",
        "cove://commands/nook.resize",
        "cove://commands/nook.kill",
        "cove://commands/nook.rename",
        "cove://commands/nook.search",
        "cove://commands/nook.read",
        "cove://commands/nook.checkpoint",
        "cove://commands/nook.subscribe",
        "cove://commands/nook.scope.get",
        "cove://commands/send_to_agent",
        "cove://commands/agent.message",
        "cove://commands/canvas.action",
        "cove://commands/browser.open",
        "cove://commands/browser.navigate",
        "cove://commands/browser.back",
        "cove://commands/browser.forward",
        "cove://commands/browser.reload",
        "cove://commands/browser.close",
        "cove://commands/browser.snapshot",
        "cove://commands/browser.click",
        "cove://commands/browser.fill",
        "cove://commands/browser.eval",
        "cove://commands/browser.screenshot",
        "cove://commands/browser.setUserAgent",
        "cove://commands/browser.clear",
        "cove://commands/browser.type",
        "cove://commands/browser.press",
        "cove://commands/browser.select",
        "cove://commands/browser.scroll",
        "cove://commands/browser.wait",
        "cove://commands/browser.get",
        "cove://commands/browser.is",
        "cove://commands/workspace.context"
    };

    public static TheoryData<string> ExplicitDomainCommandUris => new()
    {
        "cove://commands/nook.list",
        "cove://commands/nook.spawn",
        "cove://commands/nook.scope.set",
        "cove://commands/browser.create",
        "cove://commands/browser.automation.result",
        "cove://commands/knowledge.ping",
        "cove://commands/note.create",
        "cove://commands/note.get",
        "cove://commands/note.list",
        "cove://commands/note.update",
        "cove://commands/note.delete",
        "cove://commands/note.search",
        "cove://commands/note.read",
        "cove://commands/note.write",
        "cove://commands/note.history",
        "cove://commands/note.media.save",
        "cove://commands/note.get-state",
        "cove://commands/note.save-state",
        "cove://commands/timeline.append",
        "cove://commands/timeline.list",
        "cove://commands/blackboard.post",
        "cove://commands/blackboard.show",
        "cove://commands/memory.add",
        "cove://commands/memory.search",
        "cove://commands/memory.recall",
        "cove://commands/memory.show",
        "cove://commands/memory.supersede",
        "cove://commands/memory.reindex",
        "cove://commands/memory.consolidate",
        "cove://commands/memory.propose",
        "cove://commands/memory.proposal.transition",
        "cove://commands/edits.find",
        "cove://commands/vault.search",
        "cove://commands/vault.resume",
        "cove://commands/vault.set-setting",
        "cove://commands/vault.reindex",
        "cove://commands/library.list",
        "cove://commands/library.materialize",
        "cove://commands/review.add-comment",
        "cove://commands/review.list-comments",
        "cove://commands/review.resolve",
        "cove://commands/review.reopen",
        "cove://commands/review.close",
        "cove://commands/review.re-anchor",
        "cove://commands/review.audit",
        "cove://commands/review.telemetry",
        "cove://commands/attribution.record",
        "cove://commands/attribution.find-by-line",
        "cove://commands/attribution.find-by-range",
        "cove://commands/attribution.find-by-tool-use",
        "cove://commands/review.dispatch"
    };

    [Theory]
    [MemberData(nameof(ScopedCommandUris))]
    public void ScopedCommands_AreRepresentedByTheAuthorizationPolicy(string uri)
    {
        Assert.True(ScopeEnforcement.IsNookTargetingVerb(uri), uri);
    }

    [Theory]
    [MemberData(nameof(ScopedCommandUris))]
    [MemberData(nameof(ExplicitDomainCommandUris))]
    public void SecurityDomainCommands_AreExplicitlyRepresented(string uri)
    {
        Assert.True(ScopeEnforcement.IsRepresentedVerb(uri), uri);
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task NookWrite_CrossNook_SameTabScope_ReturnsAccessDenied()
    {
        using var nooks = NewNooks();
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
            if (!string.IsNullOrEmpty(callerNook)) nooks.Kill(callerNook);
            if (!string.IsNullOrEmpty(targetNook)) nooks.Kill(targetNook);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task NookWrite_SameNook_SameTabScope_Allowed()
    {
        using var nooks = NewNooks();
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
        finally { if (!string.IsNullOrEmpty(nook)) nooks.Kill(nook); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task NookWrite_CrossNook_AllScope_Allowed()
    {
        using var nooks = NewNooks();
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
            if (!string.IsNullOrEmpty(callerNook)) nooks.Kill(callerNook);
            if (!string.IsNullOrEmpty(targetNook)) nooks.Kill(targetNook);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task NookWrite_NoCallerNookId_Allowed()
    {
        using var nooks = NewNooks();
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
        finally { if (!string.IsNullOrEmpty(nook)) nooks.Kill(nook); }
    }

    [Fact]
    public void WorkspaceContext_NoExplicitTarget_DefaultsToCaller()
    {
        var scopeStore = new NookScopeStore(NewDir(), NullLogger.Instance);
        scopeStore.SetScope("caller-nook", McpScope.SameTab);
        var request = new Cove.Protocol.ControlRequest(
            "1",
            "cove://commands/workspace.context",
            CallerNookId: "caller-nook");

        var response = ScopeEnforcement.Check(
            request,
            scopeStore,
            null,
            null,
            null);

        Assert.Null(response);
    }

    [Fact]
    public void WorkspaceContext_ExplicitForeignTarget_EnforcesScope()
    {
        var scopeStore = new NookScopeStore(NewDir(), NullLogger.Instance);
        scopeStore.SetScope("caller-nook", McpScope.SameTab);
        var request = new Cove.Protocol.ControlRequest(
            "1",
            "cove://commands/workspace.context",
            JsonDocument.Parse("""{"nookId":"foreign-nook"}""").RootElement.Clone(),
            CallerNookId: "caller-nook");

        var response = ScopeEnforcement.Check(
            request,
            scopeStore,
            null,
            null,
            null);

        Assert.NotNull(response);
        Assert.Equal("access_denied", response!.Error?.Code);
    }
}
