using Cove.Adapters;
using Cove.Engine.Browser;
using Cove.Engine.Config;
using Cove.Engine.Dictation;
using Cove.Engine.Hooks;
using Cove.Engine.Knowledge;
using Cove.Engine.Launch;
using Cove.Engine.Lifecycle;
using Cove.Engine.Nooks;
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
        NookRegistry? nooks = null,
        Cove.Engine.Layout.LayoutService? layout = null,
        Cove.Engine.Bays.BayManager? bays = null,
        Cove.Engine.Bays.RunCommandService? runCommands = null,
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
        NookTypeRegistry? nookTypes = null,
        BrowserNookManager? browser = null,
        ConfigService? config = null,
        Cove.Adapters.AdapterManifestStore? manifestStore = null,
        Cove.Adapters.RegistryService? registry = null,
        Cove.Engine.Activity.OmniChatStore? omniChat = null,
        Cove.Engine.Protocol.NookScopeStore? nookScopes = null,
        Cove.Engine.Protocol.StateBus? stateBus = null,
        Cove.Engine.Protocol.ExtensionRegistry? extensions = null,
        Cove.Engine.Captures.CaptureStore? captures = null,
        Cove.Engine.Bays.GitReadModel? gitReadModel = null,
        Cove.Engine.Search.SearchService? searchService = null,
        Cove.Engine.Theming.ThemeService? themes = null,
        Cove.Engine.Keybindings.KeybindingEngine? keybindings = null,
        Cove.Engine.Browser.BrowserAutomationBridge? browserAutomation = null,
        Cove.Engine.Diagnostics.DiagnosticsHub? diagnostics = null,
        Cove.Engine.Diagnostics.PerformanceBundleService? perfBundles = null,
        RecentSessionStore? recentSessions = null,
        Cove.Engine.Lsp.LspService? lspService = null,
        Cove.Adapters.SessionService? sessionService = null,
        string? baysDir = null,
        Cove.Tasks.Scheduler.IScheduleMutationAcknowledger? taskScheduler = null,
        Cove.Engine.Filesystem.DirectoryListingService? directoryListing = null,
        Cove.Engine.Bays.GitSummaryService? gitSummary = null,
        Cove.Engine.Feedback.FeedbackStore? feedbackStore = null,
        Cove.Engine.Diagnostics.PerformanceResultStore? performanceResults = null,
        DictationTranscriptionRuntime? dictation = null,
        System.Threading.CancellationToken cancellationToken = default,
        System.Func<System.Threading.CancellationToken, bool>? forwardWindowFocus = null,
        System.Func<Cove.Engine.Restart.RestorationSummaryEvent?>? getRestorationSummary = null,
        System.DateTimeOffset? engineStartedAtUtc = null,
        System.Func<long>? getWorkspaceRevision = null,
        System.Action<string, System.Text.Json.JsonElement?>? emitIpcEvent = null,
        System.Func<IpcEventLog>? getIpcEvents = null,
        System.Func<bool>? startIpcMonitor = null,
        System.Func<bool>? stopIpcMonitor = null,
        System.Func<bool>? hasRenderClient = null)
    {
        try
        {
            if (nookScopes is not null)
            {
                var denied =
                    ScopeEnforcement.AuthorizeAttributedNook(
                        request,
                        nookScopes,
                        bays,
                        layout,
                        agentRouter);
                if (denied is not null)
                    return denied;
            }
            if (!CoveCommandRegistry.Handlers.TryGetValue(
                    request.Uri,
                    out var handler))
            {
                return null;
            }
            var typed =
                (System.Func<
                    EngineDispatchContext,
                    System.Threading.Tasks.Task<ControlResponse>>)
                handler;
            var dispatchCtx = new EngineDispatchContext(request, nooks, layout, bays, runCommands, restoration, snapshots, skills, agents, launchProfiles, adapterEnv, hookServer, hookRouter, agentRouter, activity, sessions, lifecycle, launcher, taskService, dispatchSaga, resumeSaga, timeline, blackboard, noteFiles, memory, memoryRanker, proposals, consolidator, edits, corpus, vaultSettings, library, reviews, attribution, reviewDispatcher, nookTypes, browser, config, manifestStore, registry, omniChat, nookScopes, stateBus, extensions, captures, gitReadModel, searchService, themes, keybindings, browserAutomation, diagnostics, perfBundles, recentSessions, lspService, sessionService, baysDir, taskScheduler, directoryListing, gitSummary, feedbackStore, performanceResults, dictation, cancellationToken, forwardWindowFocus, getRestorationSummary, engineStartedAtUtc, getWorkspaceRevision, emitIpcEvent, getIpcEvents, startIpcMonitor, stopIpcMonitor, hasRenderClient);
            dispatchCtx.Redrive = subReq => RouteAsync(subReq, nooks, layout, bays, runCommands, restoration, snapshots, skills, agents, launchProfiles, adapterEnv, hookServer, hookRouter, agentRouter, activity, sessions, lifecycle, launcher, taskService, dispatchSaga, resumeSaga, timeline, blackboard, noteFiles, memory, memoryRanker, proposals, consolidator, edits, corpus, vaultSettings, library, reviews, attribution, reviewDispatcher, nookTypes, browser, config, manifestStore, registry, omniChat, nookScopes, stateBus, extensions, captures, gitReadModel, searchService, themes, keybindings, browserAutomation, diagnostics, perfBundles, recentSessions, lspService, sessionService, baysDir, taskScheduler, directoryListing, gitSummary, feedbackStore, performanceResults, dictation, cancellationToken, forwardWindowFocus, getRestorationSummary, engineStartedAtUtc, getWorkspaceRevision, emitIpcEvent, getIpcEvents, startIpcMonitor, stopIpcMonitor, hasRenderClient);
            return await typed(dispatchCtx);
        }
        catch (System.Exception ex)
        {
            return new ControlResponse(request.Id, false, null, new ControlError("handler_error", ex.Message));
        }
    }
}
