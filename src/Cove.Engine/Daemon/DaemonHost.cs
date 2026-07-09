using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine.Config;
using Cove.Engine.Hooks;
using Cove.Engine.Pty;
using Cove.Platform;
using Cove.Platform.Ipc;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Daemon;

public sealed class DaemonHost
{
    private readonly DaemonPaths _paths;
    private readonly IControlEndpoint _endpoint;
    private readonly bool _exitWhenIdle;
    private readonly string _engineVersion = CoveBuild.InformationalVersion;
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _guiLock = new();
    private readonly List<FrameConnection> _guiConnections = new();

    private int _totalConnections;
    private int _activeConnections;
    private long _lastActivityTicks;

    private IPtyHost? _ptyHost;
    private PaneRegistry? _panes;
    private Cove.Engine.Layout.LayoutService? _layout;
    private System.Threading.Timer? _scrollbackTimer;
    private Cove.Engine.Workspaces.WorkspaceManager? _workspaces;
    private Cove.Engine.Workspaces.RunCommandService? _runCommands;
    private Cove.Engine.Restart.RestorationService? _restoration;
    private Cove.Engine.Snapshots.SnapshotService? _snapshots;
    private Cove.Engine.Skills.SkillsService? _skills;
    private Cove.Adapters.AgentDefinitionStore? _agents;
    private Cove.Adapters.LaunchProfileStore? _launchProfiles;
    private Cove.Adapters.AdapterEnvStore? _adapterEnv;
    private Cove.Engine.Adapters.EnvPropagationService? _envPropagation;
    private Cove.Adapters.AdapterReloadWatcher? _adapterReloadWatcher;
    private Cove.Adapters.AdapterManifestStore? _manifestStore;
    private Cove.Adapters.RegistryService? _registry;
    private Cove.Engine.Hooks.HookEnvelopeMatrix? _hookMatrix;
    private Cove.Engine.Hooks.ContextInjector? _hookInjector;
    private Cove.Engine.Hooks.HookHttpServer? _hookServer;
    private Cove.Engine.Hooks.HookEventRouter? _hookRouter;
    private Cove.Engine.Agents.AgentMessageRouter? _agentRouter;
    private Cove.Engine.Activity.ActivityAggregate? _activity;
    private Cove.Engine.Hooks.NeedsInputSignaler? _needsInputSignaler;
    private Cove.Engine.Notifications.NotificationPolicyEngine? _notificationPolicy;
    private Cove.Engine.Sessions.SessionResumeOrchestrator? _sessions;
    private Cove.Engine.Activity.OmniChatStore? _omniChat;
    private Cove.Engine.Protocol.PaneScopeStore? _paneScopes;
    private Cove.Engine.Protocol.StateBus? _stateBus;
    private Cove.Engine.Protocol.ExtensionRegistry? _extensions;
    private Cove.Engine.Lifecycle.AgentLifecycleController? _lifecycle;
    private Cove.Engine.Launch.LaunchOrchestrator? _launcher;
    private Cove.Tasks.TaskService? _taskService;
    private Cove.Tasks.Dispatch.DispatchSaga? _dispatchSaga;
    private Cove.Tasks.Dispatch.ResumeSaga? _resumeSaga;
    private Cove.Tasks.Scheduler.TaskSchedulerEngine? _scheduler;
    private Cove.Engine.Knowledge.TimelineStore? _timeline;
    private Cove.Engine.Knowledge.BlackboardStore? _blackboard;
    private Cove.Engine.Knowledge.NoteFileStore? _noteFiles;
    private Cove.Engine.Knowledge.NoteSnapshotService? _noteSnapshots;
    private Cove.Engine.Knowledge.MemoryStore? _memory;
    private Cove.Engine.Knowledge.MemoryRanker? _memoryRanker;
    private Cove.Engine.Knowledge.ProposalStore? _proposals;
    private Cove.Engine.Knowledge.MemoryConsolidator? _consolidator;
    private Cove.Engine.Knowledge.EditsIndex? _edits;
    private Cove.Engine.Knowledge.SessionCorpusIndexer? _corpus;
    private Cove.Engine.Knowledge.VaultSettingsStore? _vaultSettings;
    private Cove.Engine.Knowledge.LibraryStore? _library;
    private Cove.Engine.Knowledge.ReviewStore? _reviews;
    private Cove.Engine.Knowledge.AttributionIndex? _attribution;
    private Cove.Engine.Knowledge.ReviewDispatcher? _reviewDispatcher;
    private Cove.Engine.Panes.PaneTypeRegistry? _paneTypes;
    private Cove.Engine.Browser.BrowserPaneManager? _browser;
    private Cove.Engine.Config.ConfigService? _config;
    private Cove.Engine.Captures.CaptureStore? _captures;
    private Cove.Engine.Diagnostics.DiagnosticsHub? _diagnostics;
    private Cove.Engine.Diagnostics.PerformanceBundleService? _perfBundles;
    private Cove.Engine.Workspaces.GitReadModel? _gitReadModel;
    private Cove.Engine.Search.SearchService? _searchService;
    private Cove.Engine.Theming.ThemeService? _themes;
    private Cove.Engine.Keybindings.KeybindingEngine? _keybindings;
    private Cove.Engine.Browser.BrowserAutomationBridge? _browserAutomation;

    public DaemonHost(DaemonPaths paths, IControlEndpoint endpoint, bool exitWhenIdle, Cove.Tasks.Dispatch.DispatchSaga? dispatchSaga = null, Cove.Tasks.Dispatch.ResumeSaga? resumeSaga = null)
    {
        _paths = paths;
        _endpoint = endpoint;
        _exitWhenIdle = exitWhenIdle;
        _dispatchSaga = dispatchSaga;
        _resumeSaga = resumeSaga;
    }
    public void SetSagas(Cove.Tasks.Dispatch.DispatchSaga? dispatchSaga, Cove.Tasks.Dispatch.ResumeSaga? resumeSaga)
    {
        _dispatchSaga = dispatchSaga;
        _resumeSaga = resumeSaga;
    }

    public async Task<int> RunAsync(CancellationToken externalCancellation)
    {
        CoveTree.Ensure(_paths.DataDir);
        using var loggerFactory = Cove.Platform.CoveLog.CreateEngineLoggerFactory(_paths.DataDir.LogsDir, _paths.Channel);
        var logger = loggerFactory.CreateLogger<DaemonHost>();

        _ptyHost = PtyHostFactory.Create(logger);
        var probedPath = Cove.Platform.LoginShellPath.Probe(logger);
        var dataDir = _paths.DataDir.Root;
        var cliPath = System.IO.Path.Combine(dataDir, "bin", "cove");
        var spawnEnv = new SpawnEnvironment(probedPath, dataDir, cliPath, "default");
        var shellDir = ShellIntegration.Install(dataDir);
        _panes = new PaneRegistry(_ptyHost, logger, spawnEnv, shellDir);
        _layout = new Cove.Engine.Layout.LayoutService();
        _workspaces = new Cove.Engine.Workspaces.WorkspaceManager();
        _runCommands = new Cove.Engine.Workspaces.RunCommandService(new Cove.Engine.Workspaces.RunCommandStore(System.IO.Path.Combine(dataDir, "run-commands"), logger), new Cove.Engine.Workspaces.PtyRunCommandSessionFactory(_ptyHost, spawnEnv, shellDir, logger), logger: logger);
        _restoration = new Cove.Engine.Restart.RestorationService(dataDir, logger, emitProgress: e => BroadcastEvent("restore.progress", e, Cove.Engine.Restart.RestorationJsonContext.Default.RestoreProgressEvent));
        _snapshots = new Cove.Engine.Snapshots.SnapshotService(dataDir, System.IO.Path.Combine(dataDir, "snapshots"), new Cove.Engine.Workspaces.ProcessGitRunner(), logger);
        _skills = new Cove.Engine.Skills.SkillsService(dataDir, logger: logger);
        _agents = new Cove.Adapters.AgentDefinitionStore(System.IO.Path.Combine(dataDir, "agents"), logger);
        _launchProfiles = new Cove.Adapters.LaunchProfileStore(System.IO.Path.Combine(dataDir, "launch-profiles"), logger);
        _adapterEnv = new Cove.Adapters.AdapterEnvStore(System.IO.Path.Combine(dataDir, "adapter-env"), logger);
        _envPropagation = new Cove.Engine.Adapters.EnvPropagationService(_adapterEnv, new Cove.Engine.Adapters.PaneRegistryEnvTarget(_panes), a => ResolveAdapterBinary(a, logger), logger);
        _hookServer = new Cove.Engine.Hooks.HookHttpServer(dataDir, logger);
        _hookRouter = new Cove.Engine.Hooks.HookEventRouter(logger);
        _agentRouter = new Cove.Engine.Agents.AgentMessageRouter();
        _activity = new Cove.Engine.Activity.ActivityAggregate(_hookRouter, _agentRouter);
        _sessions = new Cove.Engine.Sessions.SessionResumeOrchestrator(logger);
        _lifecycle = new Cove.Engine.Lifecycle.AgentLifecycleController(logger);
        _manifestStore = new Cove.Adapters.AdapterManifestStore(System.IO.Path.Combine(dataDir, "adapters"), logger);
        var registryCachePath = System.IO.Path.Combine(dataDir, "adapters", "registry.json");
        var repoRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var devRegistryPath = System.IO.Path.Combine(repoRoot, "..", "atrium-adapters", "registry.json");
        Cove.Adapters.IRegistryFetcher fetcher = System.IO.File.Exists(devRegistryPath)
            ? new Cove.Adapters.FileRegistryFetcher(devRegistryPath, logger)
            : new Cove.Adapters.HttpRegistryFetcher(Cove.Adapters.RegistryConstants.RegistryContentsUrl, logger);
        _registry = new Cove.Adapters.RegistryService(registryCachePath, fetcher);
        var resumeProtocol = new Cove.Engine.Launch.AdapterResumeProtocol(_manifestStore, new Cove.Adapters.MethodRunner(), logger);
        var resumeService = new Cove.Engine.Restart.AgentResumeService(resumeProtocol);
        _launcher = new Cove.Engine.Launch.LaunchOrchestrator(_manifestStore, new Cove.Adapters.MethodRunner(), new Cove.Adapters.BinaryDiscoveryService(), probedPath, resumeService, new Cove.Engine.Launch.LauncherOverrideStore(System.IO.Path.Combine(dataDir, "launcher-overrides"), logger), logger);
        _taskService = new Cove.Tasks.TaskService(dataDir, logger);
        _ = _taskService.StartAsync();
        var knowledgeKernel = new Knowledge.KnowledgePersistenceKernel(dataDir, logger);
        knowledgeKernel.EnsureAllSchemas();
        var restoration = new Cove.Tasks.Restart.RunRestorationService(_taskService, logger);
        var restoredSummary = restoration.RestoreOnStartup();
        if (restoredSummary.RestoredRuns.Count > 0)
            logger.LogWarning("restore: {count} non-terminal runs rehydrated as interrupted", restoredSummary.RestoredRuns.Count);
        _scheduler = new Cove.Tasks.Scheduler.TaskSchedulerEngine(_taskService, new Cove.Tasks.Schedules.CronosCronExpander(logger), new Cove.Tasks.Scheduler.SystemClock(), logger);
        _ = _scheduler.StartAsync(_shutdown.Token);
        _stateBus = new Cove.Engine.Protocol.StateBus(dataDir, logger);
        _extensions = new Cove.Engine.Protocol.ExtensionRegistry(_manifestStore!);
        _extensions.Index();
        _paneScopes = new Cove.Engine.Protocol.PaneScopeStore(dataDir, logger);
        _noteSnapshots = new Cove.Engine.Knowledge.NoteSnapshotService(dataDir, logger);
        _noteFiles = new Cove.Engine.Knowledge.NoteFileStore(dataDir, logger, _noteSnapshots);
        _timeline = new Cove.Engine.Knowledge.TimelineStore(dataDir, logger);
        _blackboard = new Cove.Engine.Knowledge.BlackboardStore(dataDir, logger);
        _memory = new Cove.Engine.Knowledge.MemoryStore(dataDir, logger);
        _memoryRanker = new Cove.Engine.Knowledge.MemoryRanker(_memory, dataDir, logger);
        _proposals = new Cove.Engine.Knowledge.ProposalStore(dataDir, logger);
        _consolidator = new Cove.Engine.Knowledge.MemoryConsolidator(_memory, _proposals, logger);
        _edits = new Cove.Engine.Knowledge.EditsIndex(dataDir, logger);
        _corpus = new Cove.Engine.Knowledge.SessionCorpusIndexer(dataDir, logger);
        _vaultSettings = new Cove.Engine.Knowledge.VaultSettingsStore(dataDir, logger);
        _library = new Cove.Engine.Knowledge.LibraryStore(dataDir, logger);
        _library.EnsureSchema();
        _reviews = new Cove.Engine.Knowledge.ReviewStore(dataDir, logger);
        _attribution = new Cove.Engine.Knowledge.AttributionIndex(dataDir, logger);
        _reviewDispatcher = new Cove.Engine.Knowledge.ReviewDispatcher(logger);
        _omniChat = new Cove.Engine.Activity.OmniChatStore(System.IO.Path.Combine(dataDir, "omni-chat"), logger);
        _browser = new Cove.Engine.Browser.BrowserPaneManager();
        _config = new Cove.Engine.Config.ConfigService(dataDir, logger);
        _captures = new Cove.Engine.Captures.CaptureStore(dataDir, logger);
        _diagnostics = new Cove.Engine.Diagnostics.DiagnosticsHub(null, logger);
        _perfBundles = new Cove.Engine.Diagnostics.PerformanceBundleService(_diagnostics, System.IO.Path.Combine(dataDir, "perf-bundles"), logger);
        _gitReadModel = new Cove.Engine.Workspaces.GitReadModel(new Cove.Engine.Workspaces.ProcessGitRunner(), logger);
        _searchService = new Cove.Engine.Search.SearchService(logger);
        _keybindings = new Cove.Engine.Keybindings.KeybindingEngine();
        Cove.Engine.Keybindings.DefaultKeymap.RegisterAll(_keybindings);
        var savedKeybindings = _config?.GetKeybindingsJson();
        if (!string.IsNullOrEmpty(savedKeybindings))
        {
            try { _keybindings.LoadFromJson(savedKeybindings); }
            catch (System.Exception ex) { logger.ConfigParseFailed("keybindings", ex.Message); }
        }
        _themes = new Cove.Engine.Theming.ThemeService(dataDir);
        _browserAutomation = new Cove.Engine.Browser.BrowserAutomationBridge(e => BroadcastEvent("browser.automation.exec", e, Cove.Protocol.CoveJsonContext.Default.BrowserAutomationExecEvent), logger);
        _config!.SettingsChanged += key => BroadcastEvent("config.changed", new ConfigChangedEvent(key), Cove.Protocol.CoveJsonContext.Default.ConfigChangedEvent);
        _hookServer.OnEvent += _hookRouter.Route;
        _paneTypes = Cove.Engine.Panes.PaneTypeRegistry.CreateWithBuiltins();
        _notificationPolicy = new Cove.Engine.Notifications.NotificationPolicyEngine(dataDir, logger);
        _needsInputSignaler = new Cove.Engine.Hooks.NeedsInputSignaler(_activity!, new DaemonNotificationBus(this), () => GetFocusedPane(), _notificationPolicy);
        _hookRouter.NeedsInputTransition += (paneId, needsInput) =>
        {
            if (needsInput) _needsInputSignaler!.CheckAndSignal(paneId);
            else _needsInputSignaler!.ClearSignal(paneId);
        };
        _hookMatrix = new Cove.Engine.Hooks.HookEnvelopeMatrix();
        PopulateHookMatrix(_hookMatrix, _manifestStore!, logger);
        _hookInjector = new Cove.Engine.Hooks.ContextInjector(_hookMatrix, ParseAwareness(_config.Get("context.awareness")), logger);
        _hookServer.Injector = _hookInjector;
        var adaptersRoot = System.IO.Path.Combine(dataDir, "adapters");
        System.IO.Directory.CreateDirectory(adaptersRoot);
        _adapterReloadWatcher = new Cove.Adapters.AdapterReloadWatcher(adaptersRoot, logger: logger);
        _adapterReloadWatcher.AdaptersChanged += () => OnAdaptersChanged(dataDir, logger);
        _adapterReloadWatcher.Start();
        var aggregator = new Cove.Engine.Hooks.AmbientContextAggregator();
        _hookServer.Aggregator = aggregator;
        await _hookServer.StartAsync();

        var wsDir = System.IO.Path.Combine(dataDir, "workspaces", "default");
        var wasClean = _restoration.WasCleanShutdown();
        _restoration.MarkLaunching();
        _restoration.EmitProgress("default", "load_workspace", Cove.Engine.Restart.RestorePhase.Started, wasClean ? "clean" : "unclean");
        var (savedLayout, sessions) = Cove.Engine.Layout.WorkspacePersistence.Load(wsDir, logger);
        if (savedLayout is { } sl)
        {
            if (!string.IsNullOrEmpty(sl.ProjectDir)) _panes!.ProjectDir = sl.ProjectDir;
            _restoration.EmitProgress("default", "load_workspace", Cove.Engine.Restart.RestorePhase.WorkspaceLoaded);
            foreach (var room in sl.Rooms)
                foreach (var leaf in Cove.Engine.Layout.MosaicOps.Leaves(room.LayoutTree))
                    if (sessions.TryGetValue(leaf.PaneId, out var d))
                    {
                        try { _panes!.RespawnAs(d.PaneId, d.Command, d.Args, d.Cwd, 80, 24, Cove.Engine.Layout.WorkspacePersistence.LoadScrollback(d.PaneId, wsDir)); if (!string.IsNullOrEmpty(d.Title)) _panes!.Rename(d.PaneId, d.Title!); }
                        catch (System.Exception ex) { logger.LogWarning(ex, "respawn on restore failed for {PaneId}", d.PaneId); }
                    }
            _restoration.EmitProgress("default", "materialize_panes", Cove.Engine.Restart.RestorePhase.PanesMaterialized);
            _layout!.LoadSnapshot(sl);
        }
        try { if (_runCommands is not null) await _runCommands.RelaunchPreviouslyRunningAsync().ConfigureAwait(false); }
        catch (System.Exception ex) { logger.LogWarning(ex, "run-command relaunch on restore failed"); }
        _restoration.EmitProgress("default", "restore_complete", Cove.Engine.Restart.RestorePhase.Completed);
        PopulateAmbientAggregator(aggregator, dataDir, logger);
        _layout!.OnChanged = () =>
        {
            try { Cove.Engine.Layout.WorkspacePersistence.Save(_layout.ToSnapshot("default", "default", _panes!.ProjectDir ?? System.Environment.CurrentDirectory), _panes!.Descriptors(), wsDir); }
            catch (System.Exception ex) { logger.LogWarning(ex, "workspace persist failed"); }
        };
        _scrollbackTimer = new System.Threading.Timer(_ => { try { if (_panes is { } reg) foreach (var info in reg.List()) { var bytes = reg.SnapshotRing(info.PaneId); if (bytes.Length > 0) Cove.Engine.Layout.WorkspacePersistence.SaveScrollback(info.PaneId, bytes, wsDir); } } catch (System.Exception ex) { logger.LogWarning(ex, "scrollback snapshot failed"); } }, null, System.TimeSpan.FromSeconds(15), System.TimeSpan.FromSeconds(15));
        SingleInstanceGuard? guard = SingleInstanceGuard.TryAcquire(_paths.PidFilePath);
        if (guard is null)
        {
            DaemonLog.Write(_paths, "daemon already running on channel " + _paths.Channel);
            return 0;
        }

        if (!OperatingSystem.IsWindows() && File.Exists(_paths.SocketPath))
        {
            if (_endpoint.TryProbe(250))
            {
                DaemonLog.Write(_paths, "stale_reclaim_conflict on channel " + _paths.Channel);
                guard.Dispose();
                return 1;
            }
            try { File.Delete(_paths.SocketPath); }
            catch (Exception ex) { DaemonLog.Write(_paths, "stale unlink failed: " + ex.Message); }
        }

        IControlListener listener;
        try
        {
            listener = _endpoint.Bind();
        }
        catch (Exception ex)
        {
            DaemonLog.Write(_paths, "bind failed (already running?): " + ex.Message);
            guard.Dispose();
            return 0;
        }

        guard.WritePid(Environment.ProcessId);
        _lastActivityTicks = DateTimeOffset.UtcNow.Ticks;
        DaemonLog.Write(_paths, $"daemon up pid={Environment.ProcessId} channel={_paths.Channel} addr={_endpoint.Address}");
        logger.DaemonStarted(System.Environment.ProcessId, _paths.Channel);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(externalCancellation, _shutdown.Token);
        Task? idle = _exitWhenIdle ? Task.Run(() => IdleMonitorAsync(linked.Token)) : null;

        try
        {
            await AcceptLoopAsync(listener, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await listener.DisposeAsync().ConfigureAwait(false);
        try { _restoration?.MarkCleanShutdown(); } catch (System.Exception ex) { logger.LogWarning(ex, "clean-shutdown marker failed"); }
        try { if (_panes is { } reg) foreach (var info in reg.List()) { var bytes = reg.SnapshotRing(info.PaneId); if (bytes.Length > 0) Cove.Engine.Layout.WorkspacePersistence.SaveScrollback(info.PaneId, bytes, wsDir); } } catch (System.Exception ex) { logger.LogWarning(ex, "shutdown scrollback snapshot failed"); }
        _scrollbackTimer?.Dispose();
        if (_runCommands is not null)
            await _runCommands.DisposeAsync().ConfigureAwait(false);
        if (_workspaces is not null)
            await _workspaces.DisposeAsync().ConfigureAwait(false);
        _panes?.Dispose();
        _skills?.Dispose();
        _adapterReloadWatcher?.Dispose();
        _envPropagation?.Dispose();
        _hookServer?.Dispose();
        if (!OperatingSystem.IsWindows())
        {
            try { File.Delete(_paths.SocketPath); } catch { }
        }
        PidFile.Delete(_paths.PidFilePath);
        guard.Dispose();
        if (idle is not null)
        {
            try { await idle.ConfigureAwait(false); } catch { }
        }
        logger.DaemonStopping(_paths.Channel);
        DaemonLog.Write(_paths, "daemon down");
        return 0;
    }

    private async Task AcceptLoopAsync(IControlListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Stream stream;
            try
            {
                stream = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                DaemonLog.Write(_paths, "accept error: " + ex.Message);
                break;
            }
            _ = HandleConnectionAsync(stream, cancellationToken);
        }
    }

    private async Task HandleConnectionAsync(Stream stream, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _totalConnections);
        MarkActive(1);
        var conn = new FrameConnection(stream);
        var state = new ConnState();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Frame? maybe = await conn.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
                if (maybe is null)
                    break;
                Frame f = maybe.Value;
                if (f.Header.Type == FrameType.Credit)
                    continue;
                if (f.Header.Type != FrameType.Request)
                {
                    await WriteErrorFrameAsync(conn, "malformed_frame", "control connection expects Request frames", null, cancellationToken).ConfigureAwait(false);
                    break;
                }
                ControlRequest req = ControlCodec.DecodeRequest(f.Payload);
                bool stop = await DispatchAsync(conn, stream, state, req, cancellationToken).ConfigureAwait(false);
                if (stop)
                    break;
            }
        }
        catch (ProtocolException pex)
        {
            try { await WriteErrorFrameAsync(conn, pex.Code, pex.Message, null, cancellationToken).ConfigureAwait(false); }
            catch { }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DaemonLog.Write(_paths, "connection error: " + ex.Message);
        }
        finally
        {
            if (state.IsGui)
                UnregisterGui(conn);
            try { await conn.DisposeAsync().ConfigureAwait(false); } catch { }
            MarkActive(-1);
        }
    }

    private async Task<bool> DispatchAsync(FrameConnection conn, Stream stream, ConnState state, ControlRequest req, CancellationToken cancellationToken)
    {
        if (req.Uri == "cove://sys/hello")
        {
            if (req.Params is not JsonElement helloEl)
            {
                await WriteResponseAsync(conn, Fail(req.Id, "invalid_params", "hello params required"), cancellationToken).ConfigureAwait(false);
                return false;
            }
            HelloParams? hp = helloEl.Deserialize(CoveJsonContext.Default.HelloParams);
            if (hp is null)
            {
                await WriteResponseAsync(conn, Fail(req.Id, "invalid_params", "hello params malformed"), cancellationToken).ConfigureAwait(false);
                return false;
            }
            if (hp.ProtocolVersion != ProtocolConstants.SemanticProtocolVersion)
            {
                await WriteErrorFrameAsync(conn, "version_mismatch", $"protocol {hp.ProtocolVersion} unsupported", null, cancellationToken).ConfigureAwait(false);
                return true;
            }
            state.HelloDone = true;
            if (hp.ClientKind == "gui")
            {
                state.IsGui = true;
                RegisterGui(conn);
            }
            var hr = new HelloResult(ProtocolConstants.SemanticProtocolVersion, _engineVersion, Environment.ProcessId, _paths.Channel);
            await WriteResponseAsync(conn, new ControlResponse(req.Id, true, ToElement(hr, CoveJsonContext.Default.HelloResult)), cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (!state.HelloDone)
        {
            await WriteResponseAsync(conn, Fail(req.Id, "not_ready", "sys/hello required before commands"), cancellationToken).ConfigureAwait(false);
            return false;
        }
        ControlResponse? generated = await Cove.Engine.EngineCommandRouter.RouteAsync(req, _panes, _layout, _workspaces, _runCommands, _restoration, _snapshots, _skills, _agents, _launchProfiles, _adapterEnv, _hookServer, _hookRouter, _agentRouter, _activity, _sessions, _lifecycle, _launcher, _taskService, _dispatchSaga, _resumeSaga, _timeline, _blackboard, _noteFiles, _memory, _memoryRanker, _proposals, _consolidator, _edits, _corpus, _vaultSettings, _library, _reviews, _attribution, _reviewDispatcher, _paneTypes, _browser, _config, _manifestStore, _registry, _omniChat, _paneScopes, _stateBus, _extensions, _captures, _gitReadModel, _searchService, _themes, _keybindings, _browserAutomation, _diagnostics, _perfBundles, cancellationToken).ConfigureAwait(false);
        if (generated is not null)
        {
            if (generated.Ok && IsMutatingVerb(req.Uri))
            {
                BroadcastEvent("state.changed", new StateChangedEvent(req.Uri), Cove.Protocol.CoveJsonContext.Default.StateChangedEvent);
                var taskChannel = ResolveTaskEventChannel(req.Uri);
                if (taskChannel is not null)
                    BroadcastEvent(taskChannel, new StateChangedEvent(req.Uri), Cove.Protocol.CoveJsonContext.Default.StateChangedEvent);
            }
            await WriteResponseAsync(conn, generated, cancellationToken).ConfigureAwait(false);
            return false;
        }
        if (req.Uri.StartsWith("cove://state/", System.StringComparison.Ordinal))
        {
            await HandleStateUriAsync(conn, req, cancellationToken).ConfigureAwait(false);
            return false;
        }

        switch (req.Uri)
        {
            case "cove://sys/ping":
                await WriteResponseAsync(conn, new ControlResponse(req.Id, true, Parse("{\"pong\":true}")), cancellationToken).ConfigureAwait(false);
                return false;

            case "cove://sys/daemon.status":
                {
                    var status = new DaemonStatusResult(
                        Environment.ProcessId,
                        _paths.Channel,
                        _engineVersion,
                        Volatile.Read(ref _totalConnections),
                        0,
                        (long)(DateTimeOffset.UtcNow - _startedAtUtc).TotalSeconds);
                    await WriteResponseAsync(conn, new ControlResponse(req.Id, true, ToElement(status, CoveJsonContext.Default.DaemonStatusResult)), cancellationToken).ConfigureAwait(false);
                    return false;
                }

            case "cove://sys/daemon.stop":
                await WriteResponseAsync(conn, new ControlResponse(req.Id, true, Parse("{\"stopping\":true}")), cancellationToken).ConfigureAwait(false);
                _shutdown.Cancel();
                return true;

            case "cove://commands/window.focus":
                {
                    bool focused = TryForwardFocus(cancellationToken);
                    if (focused && _workspaces is not null && _workspaces.Registry.FocusedWorkspaceId is { } focusedWs)
                        _ = _workspaces.RefreshWorktreesAsync(focusedWs);
                    JsonElement data = focused
                        ? Parse("{\"focused\":true}")
                        : Parse("{\"focused\":false,\"reason\":\"no_render_client\"}");
                    await WriteResponseAsync(conn, new ControlResponse(req.Id, true, data), cancellationToken).ConfigureAwait(false);
                    return false;
                }

            case "cove://commands/pane.subscribe":
                await StreamPaneAsync(conn, stream, req, cancellationToken).ConfigureAwait(false);
                return true;

            default:
                await WriteResponseAsync(conn, Fail(req.Id, "not_found", $"unknown command {req.Uri}"), cancellationToken).ConfigureAwait(false);
                return false;
        }
    }

    private async Task HandleStateUriAsync(FrameConnection conn, ControlRequest req, CancellationToken cancellationToken)
    {
        if (_stateBus is null)
        {
            await WriteResponseAsync(conn, Fail(req.Id, "not_ready", "state bus unavailable"), cancellationToken).ConfigureAwait(false);
            return;
        }
        var path = req.Uri["cove://state/".Length..];
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            await WriteResponseAsync(conn, Fail(req.Id, "invalid_params", "cove://state/<scope>/<namespace>[/<id>] required"), cancellationToken).ConfigureAwait(false);
            return;
        }
        var scope = parts[0];
        var ns = parts[1];
        var id = parts.Length > 2 ? parts[2] : "default";
        if (!Cove.Engine.Protocol.StateBus.IsValidScope(scope))
        {
            await WriteResponseAsync(conn, Fail(req.Id, "invalid_params", "scope must be app, workspace, tab, or pane"), cancellationToken).ConfigureAwait(false);
            return;
        }
        JsonElement valProp = default;
        bool isWrite = req.Params is JsonElement we && we.TryGetProperty("value", out valProp);
        if (isWrite)
        {
            string? rawValue = valProp.ValueKind == System.Text.Json.JsonValueKind.Null ? null : valProp.ValueKind == System.Text.Json.JsonValueKind.String ? valProp.GetString() : valProp.GetRawText();
            _stateBus.Write(scope, ns, id, rawValue);
            await WriteResponseAsync(conn, new ControlResponse(req.Id, true, Parse("{\"ok\":true}")), cancellationToken).ConfigureAwait(false);
            return;
        }
        var (exists, val) = _stateBus.Read(scope, ns, id);
        using var buf = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buf))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("exists", exists);
            if (exists && val is not null)
                writer.WriteString("value", val);
            else
                writer.WriteNull("value");
            writer.WriteEndObject();
            writer.Flush();
        }
        var data = System.Text.Json.JsonDocument.Parse(buf.ToArray()).RootElement.Clone();
        await WriteResponseAsync(conn, new ControlResponse(req.Id, true, data), cancellationToken).ConfigureAwait(false);
    }

    private async Task StreamPaneAsync(FrameConnection conn, Stream stream, ControlRequest req, CancellationToken cancellationToken)
    {
        if (_panes is null || req.Params is not JsonElement el
            || el.Deserialize(CoveJsonContext.Default.SubscribeParams) is not { } sp)
        {
            await WriteResponseAsync(conn, Fail(req.Id, "invalid_params", "subscribe params required"), cancellationToken).ConfigureAwait(false);
            return;
        }
        if (!_panes.TryGet(sp.PaneId, out PaneSession pane))
        {
            await WriteResponseAsync(conn, Fail(req.Id, "not_found", $"unknown pane {sp.PaneId}"), cancellationToken).ConfigureAwait(false);
            return;
        }

        const ulong streamId = 1;
        long head = pane.Ring.Head;
        long tail = pane.Ring.Tail;
        long baseOffset = Math.Clamp((long)sp.SinceOffset, tail, head);
        var subResult = new SubscribeResult(streamId, (ulong)baseOffset, ProtocolConstants.FlowWindow);
        await WriteResponseAsync(conn, new ControlResponse(req.Id, true, ToElement(subResult, CoveJsonContext.Default.SubscribeResult)), cancellationToken).ConfigureAwait(false);

        var sink = new SocketByteStreamSink(stream);
        var sender = new PtyStreamSender(streamId, pane.Session.SessionId, pane.Ring, baseOffset, sink);
        var gate = new object();
        bool childMarked = false;

        using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task creditLoop = Task.Run(async () =>
        {
            try
            {
                while (!streamCts.IsCancellationRequested)
                {
                    Frame? maybe = await conn.ReadFrameAsync(streamCts.Token).ConfigureAwait(false);
                    if (maybe is null)
                        break;
                    Frame f = maybe.Value;
                    if (f.Header.Type == FrameType.Credit && f.Payload.Length >= 8)
                    {
                        ulong ack = BinaryPrimitives.ReadUInt64LittleEndian(f.Payload);
                        lock (gate)
                            sender.OnCredit(ack);
                    }
                    pane.Signal.Set();
                }
            }
            catch
            {
            }
            finally
            {
                streamCts.Cancel();
                pane.Signal.Set();
            }
        });

        try
        {
            while (!streamCts.IsCancellationRequested)
            {
                Task wait = pane.Signal.WaitAsync();
                lock (gate)
                {
                    if (!childMarked && pane.Reader.HasCompleted)
                    {
                        sender.MarkChildExited(pane.Reader.ExitCode);
                        childMarked = true;
                    }
                    sender.PumpAvailable();
                }
                if (sender.Ended || sender.Faulted)
                    break;
                try { await wait.WaitAsync(streamCts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            streamCts.Cancel();
            try { await creditLoop.ConfigureAwait(false); } catch { }
        }
    }

    private bool TryForwardFocus(CancellationToken cancellationToken)
    {
        FrameConnection? gui;
        lock (_guiLock)
            gui = _guiConnections.Count > 0 ? _guiConnections[0] : null;
        if (gui is null)
            return false;
        _ = gui.WriteFrameAsync(FrameType.Event, 0, ControlCodec.Encode(new ControlEvent("window.focus", Parse("{}"))), cancellationToken);
        return true;
    }

    private string? ResolveAdapterBinary(string adapter, ILogger logger)
    {
        return _manifestStore?.Load(adapter)?.Binary;
    }

    private static Cove.Engine.Hooks.AwarenessLevel ParseAwareness(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "off" => Cove.Engine.Hooks.AwarenessLevel.Off,
            "minimal" => Cove.Engine.Hooks.AwarenessLevel.Minimal,
            _ => Cove.Engine.Hooks.AwarenessLevel.Full,
        };
    }

    private string? GetFocusedPane()
    {
        if (_workspaces is null || _layout is null)
            return null;
        var focusedWs = _workspaces.Registry.FocusedWorkspaceId;
        if (focusedWs is null || _workspaces.Get(focusedWs) is not { } actor)
            return null;
        var roomId = actor.State.ActiveRoomId;
        return roomId is null ? null : _layout.GetActive(roomId);
    }
    private void BroadcastEvent<T>(string channel, T payload, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        FrameConnection[] guis;
        lock (_guiLock)
            guis = _guiConnections.ToArray();
        if (guis.Length == 0)
            return;
        var element = System.Text.Json.JsonSerializer.SerializeToElement(payload, typeInfo);
        var frame = ControlCodec.Encode(new ControlEvent(channel, element));

        foreach (var gui in guis)
            _ = gui.WriteFrameAsync(FrameType.Event, 0, frame, _shutdown.Token);
    }

    private static bool IsMutatingVerb(string uri)
    {
        return uri.StartsWith("cove://commands/workspace.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/room.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/wing.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/collection.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/resident.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/worktree.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/workspace-command.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/task.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/run.", System.StringComparison.Ordinal);
    }

    private static string? ResolveTaskEventChannel(string uri)
    {
        if (uri.StartsWith("cove://commands/task.", System.StringComparison.Ordinal))
        {
            if (uri.Contains("status.") || uri.Contains("label."))
                return "task.board.invalidated";
            return "task.card.changed";
        }
        if (uri.StartsWith("cove://commands/run.", System.StringComparison.Ordinal))
            return "task.run.changed";
        return null;
    }
    private static void PopulateHookMatrix(Cove.Engine.Hooks.HookEnvelopeMatrix matrix, Cove.Adapters.AdapterManifestStore manifestStore, ILogger logger)
    {
        foreach (var manifest in manifestStore.LoadAll())
            matrix.RegisterFromManifest(manifest);
    }

    private void OnAdaptersChanged(string dataDir, ILogger logger)
    {
        if (_hookInjector is null)
            return;
        var fresh = new Cove.Engine.Hooks.HookEnvelopeMatrix();
        PopulateHookMatrix(fresh, _manifestStore!, logger);
        _hookInjector.SwapMatrix(fresh);
        BroadcastEvent("state.changed", new Cove.Protocol.StateChangedEvent("cove://events/adapters.changed"), Cove.Protocol.CoveJsonContext.Default.StateChangedEvent);
    }
    private void PopulateAmbientAggregator(Cove.Engine.Hooks.AmbientContextAggregator aggregator, string dataDir, ILogger logger)
    {
        var primerPath = System.IO.Path.Combine(dataDir, "cove-context.md");
        aggregator.Add("sessionStartManifest", new Cove.Engine.Hooks.SessionStartContextProvider(
            primer: () => System.IO.File.Exists(primerPath) ? System.IO.File.ReadAllText(primerPath) : "",
            skillsManifest: () => BuildSkillsManifest(),
            agentPackaging: () => ""));
        aggregator.Add("userPromptSubmit", new Cove.Engine.Hooks.LocationContextProvider(
            room: () => "default",
            wing: () => null,
            workspace: () => "default",
            otherPanes: () => _panes?.List().Select(p => p.PaneId).ToList() ?? new List<string>()));
        aggregator.Add("preToolUse", new Cove.Engine.Hooks.RunCommandContextProvider(
            runningCommands: () => GetRunningCommands(logger)));
    }

    private string BuildSkillsManifest()
    {
        if (_skills is null)
            return "";
        var entries = _skills.List();
        if (entries.Count == 0)
            return "";
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var skill in entries)
            {
                writer.WriteStartObject();
                writer.WriteString("name", skill.Name);
                writer.WriteString("source", skill.Source.ToString().ToLowerInvariant());
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.Flush();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private IReadOnlyList<string> GetRunningCommands(ILogger logger)
    {
        if (_runCommands is null)
            return System.Array.Empty<string>();
        try
        {
            return _runCommands.ListEffectiveAsync("default", null).GetAwaiter().GetResult()
                .Where(c => c.Lifecycle == Cove.Engine.Workspaces.RunCommandLifecycle.Running)
                .Select(c => c.Definition.Label)
                .ToList();
        }
        catch (System.Exception ex)
        {
            logger.AmbientRunCommandFailed(ex.Message);
            return System.Array.Empty<string>();
        }
    }

    private async Task IdleMonitorAsync(CancellationToken cancellationToken)
    {
        long idleTicks = TimeSpan.FromSeconds(ProtocolConstants.IdleExitSeconds).Ticks;
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await Task.Delay(1000, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            if (Volatile.Read(ref _activeConnections) > 0)
                continue;
            if (DateTimeOffset.UtcNow.Ticks - Volatile.Read(ref _lastActivityTicks) >= idleTicks)
            {
                DaemonLog.Write(_paths, "idle-exit");
                _shutdown.Cancel();
                break;
            }
        }
    }

    private void MarkActive(int delta)
    {
        Interlocked.Add(ref _activeConnections, delta);
        Volatile.Write(ref _lastActivityTicks, DateTimeOffset.UtcNow.Ticks);
    }

    private void RegisterGui(FrameConnection conn)
    {
        lock (_guiLock)
            _guiConnections.Add(conn);
    }

    private void UnregisterGui(FrameConnection conn)
    {
        lock (_guiLock)
            _guiConnections.Remove(conn);
    }

    private static ControlResponse Fail(string id, string code, string message) =>
        new(id, false, null, new ControlError(code, message));

    private static JsonElement ToElement<T>(T value, JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.SerializeToElement(value, typeInfo);

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static ValueTask WriteResponseAsync(FrameConnection conn, ControlResponse resp, CancellationToken cancellationToken) =>
        conn.WriteFrameAsync(FrameType.Response, 0, ControlCodec.Encode(resp), cancellationToken);

    private static ValueTask WriteErrorFrameAsync(FrameConnection conn, string code, string message, ulong? streamId, CancellationToken cancellationToken) =>
        conn.WriteFrameAsync(FrameType.Error, 0, ControlCodec.Encode(new ControlErrorFrame(code, message, streamId)), cancellationToken);

    private sealed class ConnState
    {
        public bool HelloDone;
        public bool IsGui;
    }

    private sealed class DaemonNotificationBus : Cove.Engine.Hooks.INotificationBus
    {
        private readonly DaemonHost _host;

        public DaemonNotificationBus(DaemonHost host) => _host = host;

        public void BroadcastNeedsInputSignal(string paneId, string adapter)
            => _host.BroadcastEvent("needs-input.signal", new Cove.Protocol.NeedsInputSignalDto(paneId, adapter), Cove.Protocol.CoveJsonContext.Default.NeedsInputSignalDto);

        public void BroadcastDockBadge(string paneId, string adapter)
            => _host.BroadcastEvent("dock.badge", new Cove.Protocol.NeedsInputSignalDto(paneId, adapter), Cove.Protocol.CoveJsonContext.Default.NeedsInputSignalDto);

        public void ClearNeedsInputSignal(string paneId)
            => _host.BroadcastEvent("needs-input.clear", new Cove.Protocol.NeedsInputSignalDto(paneId, ""), Cove.Protocol.CoveJsonContext.Default.NeedsInputSignalDto);

        public void ClearDockBadge()
            => _host.BroadcastEvent("dock.badge.clear", new Cove.Protocol.NeedsInputSignalDto("", ""), Cove.Protocol.CoveJsonContext.Default.NeedsInputSignalDto);

        public void DeliverNotification(string id, string title, string body, string paneId)
            => _host.BroadcastEvent("notification.deliver", new Cove.Protocol.NotificationDeliverDto(id, title, body, paneId), Cove.Protocol.CoveJsonContext.Default.NotificationDeliverDto);
    }
}
