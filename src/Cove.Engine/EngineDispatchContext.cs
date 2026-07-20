using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Adapters;
using Cove.Engine.Activity;
using Cove.Engine.Agents;
using Cove.Engine.Browser;
using Cove.Engine.Captures;
using Cove.Engine.Config;
using Cove.Engine.Dictation;
using Cove.Engine.Hooks;
using Cove.Engine.Knowledge;
using Cove.Engine.Launch;
using Cove.Engine.Lifecycle;
using Cove.Engine.Nooks;
using Cove.Engine.Protocol;
using Cove.Engine.Pty;
using Cove.Engine.Restart;
using Cove.Engine.Sessions;
using Cove.Engine.Tasks;
using Cove.Protocol;

namespace Cove.Engine;

public sealed class EngineDispatchContext
{
    public EngineDispatchContext(
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
        AgentMessageRouter? agentRouter = null,
        Cove.Engine.Activity.ActivityAggregate? activity = null,
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
        AdapterManifestStore? manifestStore = null,
        RegistryService? registry = null,
        Cove.Engine.Activity.OmniChatStore? omniChat = null,
        NookScopeStore? nookScopes = null,
        Cove.Engine.Protocol.StateBus? stateBus = null,
        ExtensionRegistry? extensions = null,
        CaptureStore? captures = null,
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
        CancellationToken cancellationToken = default,
        Func<CancellationToken, bool>? forwardWindowFocus = null,
        Func<RestorationSummaryEvent?>? getRestorationSummary = null,
        DateTimeOffset? engineStartedAtUtc = null,
        Func<long>? getWorkspaceRevision = null,
        Action<string, JsonElement?>? emitIpcEvent = null,
        Func<IpcEventLog>? getIpcEvents = null,
        Func<bool>? startIpcMonitor = null,
        Func<bool>? stopIpcMonitor = null,
        Func<bool>? hasRenderClient = null)
    {
        Request = request;
        Nooks = nooks;
        Layout = bays?.Layout ?? layout;
        Bays = bays;
        RunCommands = runCommands;
        Restoration = restoration;
        Snapshots = snapshots;
        Skills = skills;
        Agents = agents;
        LaunchProfiles = launchProfiles;
        AdapterEnv = adapterEnv;
        HookServer = hookServer;
        HookRouter = hookRouter;
        AgentRouter = agentRouter;
        Activity = activity;
        Sessions = sessions;
        Lifecycle = lifecycle;
        Launcher = launcher;
        TaskService = taskService;
        DispatchSaga = dispatchSaga;
        ResumeSaga = resumeSaga;
        Timeline = timeline;
        Blackboard = blackboard;
        NoteFiles = noteFiles;
        Memory = memory;
        MemoryRanker = memoryRanker;
        Proposals = proposals;
        Consolidator = consolidator;
        Edits = edits;
        Corpus = corpus;
        VaultSettings = vaultSettings;
        Library = library;
        Reviews = reviews;
        Attribution = attribution;
        ReviewDispatcher = reviewDispatcher;
        NookTypes = nookTypes;
        Browser = browser;
        Config = config;
        ManifestStore = manifestStore;
        Registry = registry;
        OmniChat = omniChat;
        NookScopes = nookScopes;
        StateBus = stateBus;
        Extensions = extensions;
        Captures = captures;
        GitReadModel = gitReadModel;
        SearchService = searchService;
        Themes = themes;
        Keybindings = keybindings;
        BrowserAutomation = browserAutomation;
        Diagnostics = diagnostics;
        PerfBundles = perfBundles;
        DirectoryListing = directoryListing;
        GitSummary = gitSummary;
        FeedbackStore = feedbackStore;
        PerformanceResults = performanceResults;
        Dictation = dictation;
        RecentSessions = recentSessions;
        LspService = lspService;
        SessionService = sessionService;
        BaysDir = baysDir;
        TaskScheduler = taskScheduler;
        CancellationToken = cancellationToken;
        ForwardWindowFocus = forwardWindowFocus;
        GetRestorationSummary = getRestorationSummary;
        EngineStartedAtUtc = engineStartedAtUtc;
        GetWorkspaceRevision = getWorkspaceRevision;
        EmitIpcEvent = emitIpcEvent;
        GetIpcEvents = getIpcEvents;
        StartIpcMonitor = startIpcMonitor;
        StopIpcMonitor = stopIpcMonitor;
        HasRenderClient = hasRenderClient;
    }

    public ControlRequest Request { get; }
    public NookRegistry? Nooks { get; }
    public Cove.Engine.Layout.LayoutService? Layout { get; }
    public Cove.Engine.Bays.BayManager? Bays { get; }
    public Cove.Engine.Bays.RunCommandService? RunCommands { get; }
    public Cove.Engine.Restart.RestorationService? Restoration { get; }
    public Cove.Engine.Snapshots.SnapshotService? Snapshots { get; }
    public Cove.Engine.Skills.SkillsService? Skills { get; }
    public AgentDefinitionStore? Agents { get; }
    public LaunchProfileStore? LaunchProfiles { get; }
    public AdapterEnvStore? AdapterEnv { get; }
    public HookHttpServer? HookServer { get; }
    public HookEventRouter? HookRouter { get; }
    public AgentMessageRouter? AgentRouter { get; }
    public Cove.Engine.Activity.ActivityAggregate? Activity { get; }
    public SessionResumeOrchestrator? Sessions { get; }
    public AgentLifecycleController? Lifecycle { get; }
    public LaunchOrchestrator? Launcher { get; }
    public Cove.Tasks.Dispatch.ResumeSaga? ResumeSaga { get; }
    public Cove.Tasks.TaskService? TaskService { get; }
    public Cove.Tasks.Dispatch.DispatchSaga? DispatchSaga { get; }
    public Cove.Tasks.Scheduler.IScheduleMutationAcknowledger? TaskScheduler { get; }
    public TimelineStore? Timeline { get; }
    public BlackboardStore? Blackboard { get; }
    public NoteFileStore? NoteFiles { get; }
    public MemoryStore? Memory { get; }
    public MemoryRanker? MemoryRanker { get; }
    public ProposalStore? Proposals { get; }
    public MemoryConsolidator? Consolidator { get; }
    public EditsIndex? Edits { get; }
    public SessionCorpusIndexer? Corpus { get; }
    public VaultSettingsStore? VaultSettings { get; }
    public LibraryStore? Library { get; }
    public ReviewStore? Reviews { get; }
    public AttributionIndex? Attribution { get; }
    public ReviewDispatcher? ReviewDispatcher { get; }
    public NookTypeRegistry? NookTypes { get; }
    public ConfigService? Config { get; }
    public BrowserNookManager? Browser { get; }
    public AdapterManifestStore? ManifestStore { get; }
    public RegistryService? Registry { get; }
    public OmniChatStore? OmniChat { get; }
    public NookScopeStore? NookScopes { get; }
    public Cove.Engine.Protocol.StateBus? StateBus { get; }
    public Cove.Engine.Protocol.ExtensionRegistry? Extensions { get; }
    public CaptureStore? Captures { get; }
    public Cove.Engine.Bays.GitReadModel? GitReadModel { get; }
    public Cove.Engine.Theming.ThemeService? Themes { get; }
    public Cove.Engine.Keybindings.KeybindingEngine? Keybindings { get; }
    public Cove.Engine.Search.SearchService? SearchService { get; }
    public Cove.Engine.Browser.BrowserAutomationBridge? BrowserAutomation { get; }
    public Cove.Engine.Diagnostics.DiagnosticsHub? Diagnostics { get; }
    public Cove.Engine.Diagnostics.PerformanceBundleService? PerfBundles { get; }
    public Cove.Engine.Filesystem.DirectoryListingService? DirectoryListing { get; }
    public Cove.Engine.Bays.GitSummaryService? GitSummary { get; }
    public Cove.Engine.Feedback.FeedbackStore? FeedbackStore { get; }
    public Cove.Engine.Diagnostics.PerformanceResultStore? PerformanceResults { get; }
    public DictationTranscriptionRuntime? Dictation { get; }
    public RecentSessionStore? RecentSessions { get; }
    public Cove.Engine.Lsp.LspService? LspService { get; }
    public Cove.Adapters.SessionService? SessionService { get; }
    public string? BaysDir { get; }
    public CancellationToken CancellationToken { get; }
    public Func<CancellationToken, bool>? ForwardWindowFocus { get; }
    public Func<RestorationSummaryEvent?>? GetRestorationSummary { get; }
    public DateTimeOffset? EngineStartedAtUtc { get; }
    public Func<long>? GetWorkspaceRevision { get; }
    public Action<string, JsonElement?>? EmitIpcEvent { get; }
    public Func<IpcEventLog>? GetIpcEvents { get; }
    public Func<bool>? StartIpcMonitor { get; }
    public Func<bool>? StopIpcMonitor { get; }
    public Func<bool>? HasRenderClient { get; }
    public System.Func<ControlRequest, System.Threading.Tasks.Task<ControlResponse?>>? Redrive { get; set; }

    public ControlResponse Ok<T>(T data, JsonTypeInfo<T> typeInfo)
        => new ControlResponse(Request.Id, true, JsonSerializer.SerializeToElement(data, typeInfo));

    public ControlResponse Ok()
        => new ControlResponse(Request.Id, true, null);

    public ControlResponse OkJson(string json)
        => new ControlResponse(Request.Id, true, JsonDocument.Parse(json).RootElement.Clone());

    public ControlResponse Fail(string code, string message)
        => new ControlResponse(Request.Id, false, null, new ControlError(code, message));
}
