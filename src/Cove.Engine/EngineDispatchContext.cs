using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Adapters;
using Cove.Engine.Activity;
using Cove.Engine.Agents;
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
using Cove.Protocol;

namespace Cove.Engine;

public sealed class EngineDispatchContext
{
    public EngineDispatchContext(
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
        PaneTypeRegistry? paneTypes = null,
        BrowserPaneManager? browser = null,
        ConfigService? config = null,
        AdapterManifestStore? manifestStore = null,
        RegistryService? registry = null,
        Cove.Engine.Activity.OmniChatStore? omniChat = null,
        PaneScopeStore? paneScopes = null,
        StateBus? stateBus = null,
        ExtensionRegistry? extensions = null)
    {
        Request = request;
        Panes = panes;
        Layout = layout;
        Workspaces = workspaces;
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
        PaneTypes = paneTypes;
        Browser = browser;
        Config = config;
        ManifestStore = manifestStore;
        Registry = registry;
        OmniChat = omniChat;
        PaneScopes = paneScopes;
        StateBus = stateBus;
        Extensions = extensions;
    }

    public ControlRequest Request { get; }
    public PaneRegistry? Panes { get; }
    public Cove.Engine.Layout.LayoutService? Layout { get; }
    public Cove.Engine.Workspaces.WorkspaceManager? Workspaces { get; }
    public Cove.Engine.Workspaces.RunCommandService? RunCommands { get; }
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
    public PaneTypeRegistry? PaneTypes { get; }
    public ConfigService? Config { get; }
    public BrowserPaneManager? Browser { get; }
    public AdapterManifestStore? ManifestStore { get; }
    public OmniChatStore? OmniChat { get; }
    public RegistryService? Registry { get; }
    public PaneScopeStore? PaneScopes { get; }
    public StateBus? StateBus { get; }
    public ExtensionRegistry? Extensions { get; }
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
