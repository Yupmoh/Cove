using Cove.Adapters;
using Cove.Engine.Browser;
using Cove.Engine.Config;
using Cove.Engine.Hooks;
using Cove.Engine.Knowledge;
using Cove.Engine.Launch;
using Cove.Engine.Lifecycle;
using Cove.Engine.Panes;
using Cove.Engine.Protocol;
using Cove.Engine.Pty;
using Cove.Engine.Sessions;
using Cove.Engine.Tasks;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine;

public static class EngineCommandRouter
{
    public static async Task<ControlResponse?> RouteAsync(
        ControlRequest request,
        PaneRegistry? panes = null,
        Cove.Engine.Layout.LayoutService? layout = null,
        Cove.Engine.Workspaces.WorkspaceManager? workspaces = null,
        Cove.Engine.Workspaces.RunCommandService? runCommands = null,
        Cove.Engine.Restart.RestorationService? restoration = null,
        Cove.Engine.Snapshots.SnapshotService? snapshots = null,
        Cove.Engine.Skills.SkillsService? skills = null,
        AgentDefinitionStore? agents = null,
        LaunchProfileStore? launchProfiles = null,
        AdapterEnvStore? adapterEnv = null,
        HookHttpServer? hookServer = null,
        HookEventRouter? hookRouter = null,
        Agents.AgentMessageRouter? agentRouter = null,
        Activity.ActivityAggregate? activity = null,
        SessionResumeOrchestrator? sessions = null,
        AgentLifecycleController? lifecycle = null,
        LaunchOrchestrator? launcher = null,
        Cove.Tasks.TaskService? taskService = null,
        Cove.Tasks.Dispatch.DispatchSaga? dispatchSaga = null,
        Cove.Tasks.Dispatch.ResumeSaga? resumeSaga = null,
        TimelineStore? timeline = null,
        BlackboardStore? blackboard = null,
        NoteFileStore? noteFiles = null,
        MemoryStore? memory = null,
        MemoryRanker? memoryRanker = null,
        ProposalStore? proposals = null,
        MemoryConsolidator? consolidator = null,
        EditsIndex? edits = null,
        SessionCorpusIndexer? corpus = null,
        VaultSettingsStore? vaultSettings = null,
        LibraryStore? library = null,
        ReviewStore? reviews = null,
        AttributionIndex? attribution = null,
        ReviewDispatcher? reviewDispatcher = null,
        PaneTypeRegistry? paneTypes = null,
        BrowserPaneManager? browser = null,
        ConfigService? config = null,
        Cove.Adapters.AdapterManifestStore? manifestStore = null,
        Cove.Adapters.RegistryService? registry = null,
        Cove.Engine.Activity.OmniChatStore? omniChat = null,
        Cove.Engine.Protocol.PaneScopeStore? paneScopes = null,
        Cove.Engine.Protocol.StateBus? stateBus = null,
        Cove.Engine.Protocol.ExtensionRegistry? extensions = null,
        Cove.Engine.Captures.CaptureStore? captures = null,
        Cove.Engine.Workspaces.GitReadModel? gitReadModel = null,
        Cove.Engine.Search.SearchService? searchService = null,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.Func<EngineDispatchContext, System.Threading.Tasks.Task<ControlResponse>> typed;
        try
        {
            if (!CoveCommandRegistry.Handlers.TryGetValue(request.Uri, out var handler))
                return null;
            typed = (System.Func<EngineDispatchContext, System.Threading.Tasks.Task<ControlResponse>>)handler;
        }
        catch
        {
            return null;
        }
        try
        {
            if (paneScopes is not null && ScopeEnforcement.IsPaneTargetingVerb(request.Uri))
            {
                var denied = ScopeEnforcement.Check(request, paneScopes!, workspaces, layout);
                if (denied is not null)
                    return denied;
            }
        var dispatchCtx = new EngineDispatchContext(request, panes, layout, workspaces, runCommands, restoration, snapshots, skills, agents, launchProfiles, adapterEnv, hookServer, hookRouter, agentRouter, activity, sessions, lifecycle, launcher, taskService, dispatchSaga, resumeSaga, timeline, blackboard, noteFiles, memory, memoryRanker, proposals, consolidator, edits, corpus, vaultSettings, library, reviews, attribution, reviewDispatcher, paneTypes, browser, config, manifestStore, registry, omniChat, paneScopes, stateBus, extensions, captures, gitReadModel, searchService);
            dispatchCtx.Redrive = subReq => RouteAsync(subReq, panes, layout, workspaces, runCommands, restoration, snapshots, skills, agents, launchProfiles, adapterEnv, hookServer, hookRouter, agentRouter, activity, sessions, lifecycle, launcher, taskService, dispatchSaga, resumeSaga, timeline, blackboard, noteFiles, memory, memoryRanker, proposals, consolidator, edits, corpus, vaultSettings, library, reviews, attribution, reviewDispatcher, paneTypes, browser, config, manifestStore, registry, omniChat, paneScopes, stateBus, extensions, captures, gitReadModel, searchService, cancellationToken);
            return await typed(dispatchCtx);
        }
        catch (System.Exception ex)
        {
            return new ControlResponse(request.Id, false, null, new ControlError("handler_error", ex.Message));
        }
    }
}
