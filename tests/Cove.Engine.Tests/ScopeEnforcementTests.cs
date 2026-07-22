using System.Text.Json;
using Cove.Engine.Protocol;
using Cove.Engine.Layout;
using Cove.Persistence;
using Cove.Protocol;
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
        "cove://commands/nook.restart",
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
        "cove://commands/agent.launch",
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

    public static TheoryData<string, ScopePolicy> AgentControlPolicies => new()
    {
        {
            "cove://commands/nook.spawn",
            ScopePolicy.ControlOnly
        },
        {
            "cove://commands/nook.open-many",
            ScopePolicy.PlacementScoped
        },
        {
            "cove://commands/nook.close-others",
            ScopePolicy.TargetScoped
        },
        {
            "cove://commands/agent.launch",
            ScopePolicy.PlacementScoped
        },
        {
            "cove://commands/agent.list",
            ScopePolicy.ListScoped
        },
        {
            "cove://commands/agent.message",
            ScopePolicy.TargetScoped
        },
        {
            "cove://commands/agent.stop",
            ScopePolicy.TargetScoped
        },
        {
            "cove://commands/session.state",
            ScopePolicy.TargetScoped
        },
        {
            "cove://commands/layout.get",
            ScopePolicy.LayoutRead
        },
        {
            "cove://commands/layout.mutate",
            ScopePolicy.LayoutMutation
        },
        {
            "cove://commands/session.recent",
            ScopePolicy.NookAllowed
        },
        {
            "cove://commands/hook.emit",
            ScopePolicy.SelfOnly
        },
        {
            "cove://commands/launch-profile.create",
            ScopePolicy.ControlOnly
        }
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

    [Theory]
    [MemberData(nameof(AgentControlPolicies))]
    public void AgentControlCommands_HaveExplicitPolicies(
        string uri,
        ScopePolicy expected)
    {
        Assert.Equal(expected, ScopeEnforcement.PolicyFor(uri));
    }

    [Fact]
    public void EveryRegisteredAgentControlCommand_HasPolicy()
    {
        var missing = EngineCommandCatalogue.RegisteredRoutes
            .Where(ScopeEnforcement.IsAgentControlVerb)
            .Where(uri =>
                ScopeEnforcement.PolicyFor(uri)
                    == ScopePolicy.Unspecified)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void ControlPrincipal_CanAdministerEveryAgentControlCommand()
    {
        var scopeStore = new NookScopeStore(
            NewDir(),
            NullLogger.Instance);
        foreach (var uri in EngineCommandCatalogue.RegisteredRoutes
            .Where(ScopeEnforcement.IsAgentControlVerb))
        {
            var denied = ScopeEnforcement.Authorize(
                ConnectionPrincipal.Control("cli"),
                new ControlRequest("control", uri),
                scopeStore,
                null,
                null,
                null);
            Assert.Null(denied);
        }
    }

    [Fact]
    public void NookPrincipal_CannotUseRawSpawn()
    {
        var denied = ScopeEnforcement.Authorize(
            ConnectionPrincipal.Nook("cli", "caller"),
            new ControlRequest(
                "spawn",
                "cove://commands/nook.spawn",
                JsonDocument.Parse(
                    """{"command":"/bin/sleep"}""")
                    .RootElement.Clone()),
            new NookScopeStore(
                NewDir(),
                NullLogger.Instance),
            null,
            null,
            null);

        Assert.NotNull(denied);
        Assert.Equal("access_denied", denied!.Error?.Code);
    }

    [Fact]
    public void AgentLaunch_CrossBayIsDeniedBeforeDispatch()
    {
        var (layout, caller, target, _, _) =
            TwoBayLayout();
        var scopes = new NookScopeStore(
            NewDir(),
            NullLogger.Instance);
        scopes.SetScope(caller, McpScope.SameBay);
        var parameters = JsonSerializer.SerializeToElement(
            new AgentLaunchParams(
                "new",
                "omp",
                RelativeToNookId: target,
                BayId: "bay-b"),
            Cove.Protocol.CoveJsonContext.Default.AgentLaunchParams);

        var denied = ScopeEnforcement.Authorize(
            ConnectionPrincipal.Nook("cli", caller),
            new ControlRequest(
                "launch",
                "cove://commands/agent.launch",
                parameters),
            scopes,
            null,
            layout,
            null);

        Assert.NotNull(denied);
        Assert.Equal("access_denied", denied!.Error?.Code);
    }

    [Fact]
    public void LayoutMutation_CrossShoreIsDeniedBeforeDispatch()
    {
        var (layout, caller, target, targetShore) =
            SameBayTwoShoreLayout();
        var scopes = new NookScopeStore(
            NewDir(),
            NullLogger.Instance);
        scopes.SetScope(caller, McpScope.SameTab);
        var parameters = JsonSerializer.SerializeToElement(
            new LayoutMutateParams(
                "focus",
                ShoreId: targetShore,
                NookId: target),
            Cove.Protocol.CoveJsonContext.Default.LayoutMutateParams);

        var denied = ScopeEnforcement.Authorize(
            ConnectionPrincipal.Nook("cli", caller),
            new ControlRequest(
                "layout",
                "cove://commands/layout.mutate",
                parameters),
            scopes,
            null,
            layout,
            null);

        Assert.NotNull(denied);
        Assert.Equal("access_denied", denied!.Error?.Code);
    }

    [Fact]
    public void LayoutMutation_SameShoreIsAllowed()
    {
        var (layout, caller, _, _, _) = TwoBayLayout();
        var callerShore =
            layout.ResolveNookLocation(caller).ShoreId!;
        var scopes = new NookScopeStore(
            NewDir(),
            NullLogger.Instance);
        scopes.SetScope(caller, McpScope.SameTab);
        var parameters = JsonSerializer.SerializeToElement(
            new LayoutMutateParams(
                "focus",
                ShoreId: callerShore,
                NookId: caller),
            Cove.Protocol.CoveJsonContext.Default.LayoutMutateParams);

        var denied = ScopeEnforcement.Authorize(
            ConnectionPrincipal.Nook("cli", caller),
            new ControlRequest(
                "layout",
                "cove://commands/layout.mutate",
                parameters),
            scopes,
            null,
            layout,
            null);

        Assert.Null(denied);
    }

    [Fact]
    public void LayoutRead_CrossBayIsDeniedBeforeDispatch()
    {
        var (layout, caller, _, _, _) = TwoBayLayout();
        var scopes = new NookScopeStore(
            NewDir(),
            NullLogger.Instance);
        scopes.SetScope(caller, McpScope.SameBay);
        var parameters = JsonSerializer.SerializeToElement(
            new LayoutGetParams("bay-b"),
            Cove.Protocol.CoveJsonContext.Default.LayoutGetParams);

        var denied = ScopeEnforcement.Authorize(
            ConnectionPrincipal.Nook("cli", caller),
            new ControlRequest(
                "layout",
                "cove://commands/layout.get",
                parameters),
            scopes,
            null,
            layout,
            null);

        Assert.NotNull(denied);
        Assert.Equal("access_denied", denied!.Error?.Code);
    }

    [Fact]
    public async Task LayoutSnapshot_DefaultsToCallerBay()
    {
        var (layout, caller, _, _, _) = TwoBayLayout();
        var scopes = new NookScopeStore(
            NewDir(),
            NullLogger.Instance);
        scopes.SetScope(caller, McpScope.SameBay);

        var response = await EngineCommandRouter.RouteAsync(
            new ControlRequest(
                "layout",
                "cove://commands/layout.snapshot",
                CallerNookId: caller),
            layout: layout,
            nookScopes: scopes);

        Assert.True(response!.Ok, response.Error?.Message);
        var snapshot = response.Data!.Value.Deserialize(
            Cove.Persistence.CoveJsonContext.Default
                .BaySnapshot)!;
        Assert.Equal("bay-a", snapshot.Id);
    }

    [Fact]
    public void AgentList_CannotEscalatePastCallerScope()
    {
        var scopes = new NookScopeStore(
            NewDir(),
            NullLogger.Instance);
        scopes.SetScope("caller", McpScope.SameBay);
        var parameters = JsonSerializer.SerializeToElement(
            new AgentListParams("all"),
            Cove.Protocol.CoveJsonContext.Default.AgentListParams);

        var denied = ScopeEnforcement.Authorize(
            ConnectionPrincipal.Nook("cli", "caller"),
            new ControlRequest(
                "list",
                "cove://commands/agent.list",
                parameters),
            scopes,
            null,
            null,
            null);

        Assert.NotNull(denied);
        Assert.Equal("access_denied", denied!.Error?.Code);
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

    private static (
        LayoutService Layout,
        string Caller,
        string Target,
        string CallerShore,
        string TargetShore) TwoBayLayout()
    {
        const string caller = "nook-caller";
        const string target = "nook-target";
        var layout = new LayoutService();
        layout.SetActiveBay("bay-a");
        var callerShore = layout.CreateShore(
            "Caller",
            Leaf(caller));
        layout.SetActiveBay("bay-b");
        var targetShore = layout.CreateShore(
            "Target",
            Leaf(target));
        return (
            layout,
            caller,
            target,
            callerShore,
            targetShore);
    }

    private static NookLeaf Leaf(string nookId) => new()
    {
        NookId = nookId,
        Subtabs =
        [
            new Subtab(nookId, NookType.Terminal),
        ],
    };

    private static (
        LayoutService Layout,
        string Caller,
        string Target,
        string TargetShore) SameBayTwoShoreLayout()
    {
        const string caller = "nook-caller";
        const string target = "nook-target";
        var layout = new LayoutService();
        layout.SetActiveBay("bay-a");
        layout.CreateShore("Caller", Leaf(caller));
        var targetShore =
            layout.CreateShore("Target", Leaf(target));
        return (layout, caller, target, targetShore);
    }
}
