using System.Buffers.Binary;
using System.Text;
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
using ZLogger;

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
    private readonly object _dirtyBaysLock = new();
    private readonly HashSet<string> _dirtyBays = new(StringComparer.Ordinal);

    private int _totalConnections;
    private int _activeConnections;
    private long _lastActivityTicks;
    private Cove.Engine.Restart.RestorationSummaryEvent? _restorationSummary;

    private ILogger? _logger;
    private IPtyHost? _ptyHost;
    private NookRegistry? _nooks;
    private Cove.Engine.Layout.LayoutService? _layout;
    private System.Threading.Timer? _scrollbackTimer;
    private Cove.Engine.Bays.BayManager? _bays;
    private Cove.Engine.Bays.RunCommandService? _runCommands;
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
    private Cove.Engine.Sessions.RecentSessionStore? _recentSessions;
    private Cove.Engine.Activity.OmniChatStore? _omniChat;
    private Cove.Engine.Protocol.NookScopeStore? _nookScopes;
    private Cove.Engine.Protocol.StateBus? _stateBus;
    private Cove.Engine.Protocol.ExtensionRegistry? _extensions;
    private Cove.Engine.Lifecycle.AgentLifecycleController? _lifecycle;
    private Cove.Engine.Launch.LaunchOrchestrator? _launcher;
    private Cove.Tasks.TaskService? _taskService;
    private Cove.Tasks.Dispatch.DispatchSaga? _dispatchSaga;
    private Cove.Tasks.Dispatch.ResumeSaga? _resumeSaga;
    private Cove.Engine.Launch.AdapterResumeProtocol? _resumeProtocol;
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
    private Cove.Engine.Nooks.NookTypeRegistry? _nookTypes;
    private Cove.Engine.Browser.BrowserNookManager? _browser;
    private Cove.Engine.Config.ConfigService? _config;
    private Cove.Engine.Captures.CaptureStore? _captures;
    private Cove.Engine.Diagnostics.DiagnosticsHub? _diagnostics;
    private Cove.Engine.Diagnostics.PerformanceBundleService? _perfBundles;
    private Cove.Engine.Bays.GitReadModel? _gitReadModel;
    private Cove.Engine.Search.SearchService? _searchService;
    private Cove.Engine.Theming.ThemeService? _themes;
    private Cove.Engine.Keybindings.KeybindingEngine? _keybindings;
    private Cove.Engine.Browser.BrowserAutomationBridge? _browserAutomation;
    private Cove.Engine.Lsp.LspService? _lspService;
    private Cove.Adapters.SessionService? _sessionService;

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
        System.IO.Directory.CreateDirectory(_paths.DataDir.Root);
        System.IO.Directory.CreateDirectory(_paths.DataDir.IpcDir);
        System.IO.Directory.CreateDirectory(_paths.DataDir.LogsDir);
        var minimumLevel = ResolveMinimumLogLevel();
        var logPath = System.IO.Path.Combine(_paths.DataDir.LogsDir, $"{_paths.Channel}.log");
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            builder.AddZLoggerConsole();
            builder.AddZLoggerFile(logPath);
        });
        var logger = loggerFactory.CreateLogger<DaemonHost>();
        _logger = logger;
        logger.LogLevelResolved(minimumLevel.ToString(), System.Environment.GetEnvironmentVariable("COVE_LOG_LEVEL") ?? "");

        SingleInstanceGuard? dataDirLock = SingleInstanceGuard.TryAcquire(_paths.DaemonLockPath);
        if (dataDirLock is null)
        {
            int? ownerPid = PidFile.Read(_paths.DaemonLockPath);
            DaemonLog.Write(_paths, "daemon already owns data dir " + _paths.DataDir.Root + (ownerPid is { } dp ? " pid=" + dp : ""));
            logger.LogWarning("daemon already owns this data dir (pid {Pid}), exiting before touching shared state", ownerPid?.ToString() ?? "unknown");
            return 1;
        }
        dataDirLock.WritePid(Environment.ProcessId);

        CoveTree.Ensure(_paths.DataDir);

        SingleInstanceGuard? guard = SingleInstanceGuard.TryAcquire(_paths.PidFilePath);
        if (guard is null)
        {
            DaemonLog.Write(_paths, "daemon already running on channel " + _paths.Channel);
            logger.LogWarning("daemon already running on channel {Channel}, exiting before touching shared state", _paths.Channel);
            dataDirLock.Dispose();
            return 0;
        }

        _ptyHost = PtyHostFactory.Create(logger);
        var probedPath = Cove.Platform.LoginShellPath.Probe(logger);
        var dataDir = _paths.DataDir.Root;
        var cliPath = CliBinLink.LinkPath(dataDir);
        var spawnEnv = new SpawnEnvironment(probedPath, dataDir, cliPath, "default");
        var shellDir = ShellIntegration.Install(dataDir);
        _nooks = new NookRegistry(_ptyHost, logger, spawnEnv, shellDir);
        _layout = new Cove.Engine.Layout.LayoutService();
        _bays = new Cove.Engine.Bays.BayManager(emit: change =>
        {
            if (change.Kind == Cove.Engine.Bays.BayChangeKind.Updated)
                PersistBay(change.BayId, System.IO.Path.Combine(dataDir, "bays"), logger);
        }, logger: logger);
        _runCommands = new Cove.Engine.Bays.RunCommandService(new Cove.Engine.Bays.RunCommandStore(System.IO.Path.Combine(dataDir, "run-commands"), logger), new Cove.Engine.Bays.PtyRunCommandSessionFactory(_ptyHost, spawnEnv, shellDir, logger), logger: logger);
        _restoration = new Cove.Engine.Restart.RestorationService(dataDir, logger, emitProgress: e => BroadcastEvent("restore.progress", e, Cove.Engine.Restart.RestorationJsonContext.Default.RestoreProgressEvent));
        _snapshots = new Cove.Engine.Snapshots.SnapshotService(dataDir, System.IO.Path.Combine(dataDir, "snapshots"), new Cove.Engine.Bays.ProcessGitRunner(), logger);
        _skills = new Cove.Engine.Skills.SkillsService(dataDir, logger: logger);
        _agents = new Cove.Adapters.AgentDefinitionStore(System.IO.Path.Combine(dataDir, "agents"), logger);
        _launchProfiles = new Cove.Adapters.LaunchProfileStore(System.IO.Path.Combine(dataDir, "launch-profiles"), logger);
        _adapterEnv = new Cove.Adapters.AdapterEnvStore(System.IO.Path.Combine(dataDir, "adapter-env"), logger);
        _envPropagation = new Cove.Engine.Adapters.EnvPropagationService(_adapterEnv, new Cove.Engine.Adapters.NookRegistryEnvTarget(_nooks), a => ResolveAdapterBinary(a, logger), logger);
        _hookServer = new Cove.Engine.Hooks.HookHttpServer(dataDir, logger);
        _hookRouter = new Cove.Engine.Hooks.HookEventRouter(logger);
        _agentRouter = new Cove.Engine.Agents.AgentMessageRouter();
        _activity = new Cove.Engine.Activity.ActivityAggregate(_hookRouter, _agentRouter);
        _sessions = new Cove.Engine.Sessions.SessionResumeOrchestrator(logger);
        _recentSessions = new Cove.Engine.Sessions.RecentSessionStore(dataDir, logger);
        _lifecycle = new Cove.Engine.Lifecycle.AgentLifecycleController(logger);
        _manifestStore = new Cove.Adapters.AdapterManifestStore(System.IO.Path.Combine(dataDir, "adapters"), logger);
        _sessionService = new Cove.Adapters.SessionService(new Cove.Adapters.MethodRunner(logger: logger), logger: logger);
        var registryCachePath = System.IO.Path.Combine(dataDir, "adapters", "registry.json");
        var repoRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var devRegistryPath = System.IO.Path.Combine(repoRoot, "..", "atrium-adapters", "registry.json");
        Cove.Adapters.IRegistryFetcher fetcher = System.IO.File.Exists(devRegistryPath)
            ? new Cove.Adapters.FileRegistryFetcher(devRegistryPath, logger)
            : new Cove.Adapters.HttpRegistryFetcher(Cove.Adapters.RegistryConstants.RegistryContentsUrl, logger);
        _registry = new Cove.Adapters.RegistryService(registryCachePath, fetcher);
        var resumeProtocol = new Cove.Engine.Launch.AdapterResumeProtocol(_manifestStore, new Cove.Adapters.MethodRunner(logger: logger), logger);
        _resumeProtocol = resumeProtocol;
        var resumeService = new Cove.Engine.Restart.AgentResumeService(resumeProtocol);
        _launcher = new Cove.Engine.Launch.LaunchOrchestrator(_manifestStore, new Cove.Adapters.MethodRunner(logger: logger), new Cove.Adapters.BinaryDiscoveryService(logger), probedPath, resumeService, new Cove.Engine.Launch.LauncherOverrideStore(System.IO.Path.Combine(dataDir, "launcher-overrides"), logger), logger);
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
        _nookScopes = new Cove.Engine.Protocol.NookScopeStore(dataDir, logger);
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
        _browser = new Cove.Engine.Browser.BrowserNookManager();
        _config = new Cove.Engine.Config.ConfigService(dataDir, logger);
        _captures = new Cove.Engine.Captures.CaptureStore(dataDir, logger);
        var lspUserEntries = _config.GetLspServerEntries()
            .Select(e => new Cove.Engine.Lsp.LspConfigEntry(e.Languages.ToArray(), e.Command, e.Args.ToArray()))
            .ToList();
        _lspService = new Cove.Engine.Lsp.LspService(logger, lspUserEntries);
        _diagnostics = new Cove.Engine.Diagnostics.DiagnosticsHub(null, logger);
        _perfBundles = new Cove.Engine.Diagnostics.PerformanceBundleService(_diagnostics, System.IO.Path.Combine(dataDir, "perf-bundles"), logger);
        _gitReadModel = new Cove.Engine.Bays.GitReadModel(new Cove.Engine.Bays.ProcessGitRunner(), logger);
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
        var configuredTheme = _config!.GetTheme();
        var activatedTheme = _themes.SetActiveIfKnown(configuredTheme);
        if (activatedTheme is null)
            logger.LogWarning("configured theme {Theme} could not be activated, no themes available", configuredTheme);
        else if (!string.Equals(activatedTheme.Name, configuredTheme, StringComparison.Ordinal))
            logger.LogWarning("configured theme {Theme} unknown, falling back to {Fallback}", configuredTheme, activatedTheme.Name);
        _browserAutomation = new Cove.Engine.Browser.BrowserAutomationBridge(e => BroadcastEvent("browser.automation.exec", e, Cove.Protocol.CoveJsonContext.Default.BrowserAutomationExecEvent), logger);
        _config!.SettingsChanged += key => BroadcastEvent("config.changed", new ConfigChangedEvent(key), Cove.Protocol.CoveJsonContext.Default.ConfigChangedEvent);
        _hookServer.OnEvent += _hookRouter.Route;
        _nookTypes = Cove.Engine.Nooks.NookTypeRegistry.CreateWithBuiltins();
        _notificationPolicy = new Cove.Engine.Notifications.NotificationPolicyEngine(dataDir, logger);
        _needsInputSignaler = new Cove.Engine.Hooks.NeedsInputSignaler(_activity!, new DaemonNotificationBus(this), () => GetFocusedNook(), _notificationPolicy);
        _hookRouter.NeedsInputTransition += (nookId, needsInput) =>
        {
            if (needsInput) _needsInputSignaler!.CheckAndSignal(nookId);
            else _needsInputSignaler!.ClearSignal(nookId);
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

        var baysRoot = System.IO.Path.Combine(dataDir, "bays");
        var wasClean = _restoration.WasCleanShutdown();
        _restoration.MarkLaunching();
        _restoration.EmitProgress("default", "load_bay", Cove.Engine.Restart.RestorePhase.Started, wasClean ? "clean" : "unclean");
        _layout!.OnChanged = () => PersistActiveBay(baysRoot, logger);
        _nooks!.OnResized = nookId => MarkNookBayDirty(nookId);
        _hookRouter.SessionStarted += (nookId, adapter, sessionId) =>
        {
            _sessions?.SetSessionId(nookId, adapter, sessionId);
            PersistActiveBay(baysRoot, logger);
        };
        var loadedBays = Cove.Engine.Layout.BayStartup.Enumerate(baysRoot, logger);
        var fallbackProjectDir = System.Environment.CurrentDirectory;
        var restoreEnabled = _config?.GetSessionRestoreOnLaunch() ?? true;
        Cove.Engine.Restart.ResumeCommand BuildResume(string adapter, string sessionId, Cove.Engine.Restart.LauncherOverrides o)
            => resumeProtocol.BuildResumeCommandAsync(adapter, sessionId, o).GetAwaiter().GetResult();
        var restoreTotals = new Cove.Engine.Restart.RestoreSummary(0, 0, 0);
        foreach (var entry in loadedBays)
        {
            var sl = entry.Snapshot;
            var restorables = new List<Cove.Engine.Restart.RestorableNook?>();
            foreach (var shore in sl.Shores)
                foreach (var leaf in Cove.Engine.Layout.MosaicOps.Leaves(shore.LayoutTree))
                    if (entry.Sessions.TryGetValue(leaf.NookId, out var d))
                        restorables.Add(new Cove.Engine.Restart.RestorableNook(d.NookId, d.Command, d.Args, d.Cwd, d.Title, d.Adapter, d.AgentName, d.SessionId, d.Yolo, d.Cols, d.Rows));
            var spawner = new RestoreSpawner(_nooks!, entry.BayDir, sl.Id, _agentRouter, _sessions, _hookRouter, logger);
            var restorer = new Cove.Engine.Restart.SessionRestorer(spawner, BuildResume, logger);
            var summary = restorer.Restore(restorables, restoreEnabled);
            restoreTotals = new Cove.Engine.Restart.RestoreSummary(
                restoreTotals.Restored + summary.Restored,
                restoreTotals.Fresh + summary.Fresh,
                restoreTotals.Skipped + summary.Skipped);
            _layout!.LoadSnapshot(sl);
            var displayName = Cove.Engine.Layout.BayStartup.DisplayName(sl, fallbackProjectDir);
            var projectDir = string.IsNullOrWhiteSpace(sl.ProjectDir) ? fallbackProjectDir : sl.ProjectDir;
            var icon = string.IsNullOrEmpty(sl.IconKind) ? null : new Cove.Engine.Bays.BayIcon(sl.IconKind, sl.IconValue ?? "");
            await _bays!.AdoptExistingAsync(sl.Id, displayName, projectDir, icon: icon).ConfigureAwait(false);
            logger.LogWarning("bay startup: adopted {Id} '{Name}' shores={Shores} dir={Dir}", sl.Id, displayName, sl.Shores.Count, projectDir);
        }
        if (loadedBays.Count == 0)
        {
            var seedDir = _nooks!.ProjectDir ?? fallbackProjectDir;
            var seedName = System.IO.Path.GetFileName(seedDir.TrimEnd('/', '\\'));
            if (string.IsNullOrWhiteSpace(seedName)) seedName = "Bay";
            var seeded = await _bays!.CreateBayAsync(seedName, seedDir).ConfigureAwait(false);
            _layout!.SetActiveBay(seeded.Id);
            logger.LogWarning("bay startup: no persisted bays, seeded default {Id} '{Name}' dir={Dir}", seeded.Id, seedName, seedDir);
        }
        else if (_bays!.Registry.FocusedBayId is { } focused)
        {
            _layout!.SetActiveBay(focused);
            if (_bays.Get(focused) is { } focusedActor && !string.IsNullOrEmpty(focusedActor.State.ProjectDir))
                _nooks!.ProjectDir = focusedActor.State.ProjectDir;
        }
        if (restoreTotals.Restored + restoreTotals.Fresh + restoreTotals.Skipped > 0)
        {
            _restorationSummary = new Cove.Engine.Restart.RestorationSummaryEvent(restoreTotals.Restored, restoreTotals.Fresh, restoreTotals.Skipped, _startedAtUtc.ToString("o"));
            logger.LogWarning("session restoration: restored={Restored} fresh={Fresh} skipped={Skipped}", restoreTotals.Restored, restoreTotals.Fresh, restoreTotals.Skipped);
        }
        _restoration.EmitProgress("default", "materialize_nooks", Cove.Engine.Restart.RestorePhase.NooksMaterialized);
        try { if (_runCommands is not null) await _runCommands.RelaunchPreviouslyRunningAsync().ConfigureAwait(false); }
        catch (System.Exception ex) { logger.LogWarning(ex, "run-command relaunch on restore failed"); }
        _restoration.EmitProgress("default", "restore_complete", Cove.Engine.Restart.RestorePhase.Completed);
        PopulateAmbientAggregator(aggregator, dataDir, logger);
        _scrollbackTimer = new System.Threading.Timer(_ =>
        {
            PersistAllScrollback(baysRoot, logger);
            FlushDirtyBays(baysRoot, logger);
        }, null, System.TimeSpan.FromSeconds(15), System.TimeSpan.FromSeconds(15));
        if (!OperatingSystem.IsWindows() && File.Exists(_paths.SocketPath))
        {
            if (_endpoint.TryProbe(250))
            {
                DaemonLog.Write(_paths, "stale_reclaim_conflict on channel " + _paths.Channel);
                logger.LogWarning("daemon already running on channel {Channel}, exiting without publishing hook port", _paths.Channel);
                guard.Dispose();
                dataDirLock.Dispose();
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
            logger.LogWarning(ex, "daemon already running on channel {Channel}, control bind failed, exiting without publishing hook port", _paths.Channel);
            guard.Dispose();
            dataDirLock.Dispose();
            return 0;
        }

        guard.WritePid(Environment.ProcessId);
        CliBinLink.Ensure(dataDir, System.Environment.ProcessPath, logger);
        await _hookServer.PublishPortAsync().ConfigureAwait(false);
        var seedReport = Cove.Adapters.BundledAdapterSeeder.SeedFromBinaryLocation(adaptersRoot, logger);
        if (seedReport.Copied.Count + seedReport.Refreshed.Count > 0)
        {
            logger.LogInformation("bundled adapter seeding: copied={Copied} refreshed={Refreshed} userManaged={UserManaged}", seedReport.Copied.Count, seedReport.Refreshed.Count, seedReport.SkippedUserManaged.Count);
            OnAdaptersChanged(dataDir, logger);
        }
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
        try { PersistAllScrollback(System.IO.Path.Combine(_paths.DataDir.Root, "bays"), logger); } catch (System.Exception ex) { logger.LogWarning(ex, "shutdown scrollback snapshot failed"); }
        try { FlushDirtyBays(System.IO.Path.Combine(_paths.DataDir.Root, "bays"), logger); } catch (System.Exception ex) { logger.LogWarning(ex, "shutdown bay snapshot flush failed"); }
        _scrollbackTimer?.Dispose();
        if (_runCommands is not null)
            await _runCommands.DisposeAsync().ConfigureAwait(false);
        if (_bays is not null)
            await _bays.DisposeAsync().ConfigureAwait(false);
        if (_lspService is not null)
            await _lspService.DisposeAsync().ConfigureAwait(false);
        _nooks?.Dispose();
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
        dataDirLock.Dispose();
        if (idle is not null)
        {
            try { await idle.ConfigureAwait(false); } catch { }
        }
        logger.DaemonStopping(_paths.Channel);
        DaemonLog.Write(_paths, "daemon down");
        return 0;
    }

    private static LogLevel ResolveMinimumLogLevel()
    {
        var raw = System.Environment.GetEnvironmentVariable("COVE_LOG_LEVEL");
        if (string.IsNullOrWhiteSpace(raw))
            return LogLevel.Information;
        return raw.Trim().ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" or "information" => LogLevel.Information,
            "warn" or "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            _ => LogLevel.Information,
        };
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
        long dispatchStart = System.Diagnostics.Stopwatch.GetTimestamp();
        ControlResponse? generated = await Cove.Engine.EngineCommandRouter.RouteAsync(req, _nooks, _layout, _bays, _runCommands, _restoration, _snapshots, _skills, _agents, _launchProfiles, _adapterEnv, _hookServer, _hookRouter, _agentRouter, _activity, _sessions, _lifecycle, _launcher, _taskService, _dispatchSaga, _resumeSaga, _timeline, _blackboard, _noteFiles, _memory, _memoryRanker, _proposals, _consolidator, _edits, _corpus, _vaultSettings, _library, _reviews, _attribution, _reviewDispatcher, _nookTypes, _browser, _config, _manifestStore, _registry, _omniChat, _nookScopes, _stateBus, _extensions, _captures, _gitReadModel, _searchService, _themes, _keybindings, _browserAutomation, _diagnostics, _perfBundles, _recentSessions, _lspService, _sessionService, System.IO.Path.Combine(_paths.DataDir.Root, "bays"), cancellationToken).ConfigureAwait(false);
        if (generated is not null)
        {
            double dispatchMs = System.Diagnostics.Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds;
            _logger?.ControlDispatch(req.Uri, dispatchMs, generated.Ok);
            if (!generated.Ok)
                _logger?.ControlDispatchFailed(req.Uri, generated.Error?.Code ?? "", generated.Error?.Message ?? "");
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
                    if (focused && _bays is not null && _bays.Registry.FocusedBayId is { } focusedWs)
                        _ = _bays.RefreshWorktreesAsync(focusedWs);
                    JsonElement data = focused
                        ? Parse("{\"focused\":true}")
                        : Parse("{\"focused\":false,\"reason\":\"no_render_client\"}");
                    await WriteResponseAsync(conn, new ControlResponse(req.Id, true, data), cancellationToken).ConfigureAwait(false);
                    return false;
                }

            case "cove://commands/restore.summary.get":
                {
                    var s = Volatile.Read(ref _restorationSummary);
                    var result = new Cove.Engine.Restart.RestoreSummaryPullResult(
                        s is not null,
                        s?.Restored ?? 0,
                        s?.Fresh ?? 0,
                        s?.Skipped ?? 0,
                        s?.BootedAt ?? _startedAtUtc.ToString("o"));
                    await WriteResponseAsync(conn, new ControlResponse(req.Id, true, ToElement(result, Cove.Engine.Restart.RestorationSummaryJsonContext.Default.RestoreSummaryPullResult)), cancellationToken).ConfigureAwait(false);
                    return false;
                }

            case "cove://commands/nook.subscribe":
                await StreamNookAsync(conn, stream, req, cancellationToken).ConfigureAwait(false);
                return true;

            default:
                _logger?.ControlDispatchFailed(req.Uri, "not_found", "unknown command");
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
            await WriteResponseAsync(conn, Fail(req.Id, "invalid_params", "scope must be app, bay, tab, or nook"), cancellationToken).ConfigureAwait(false);
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

    private async Task StreamNookAsync(FrameConnection conn, Stream stream, ControlRequest req, CancellationToken cancellationToken)
    {
        if (_nooks is null || req.Params is not JsonElement el
            || el.Deserialize(CoveJsonContext.Default.SubscribeParams) is not { } sp)
        {
            await WriteResponseAsync(conn, Fail(req.Id, "invalid_params", "subscribe params required"), cancellationToken).ConfigureAwait(false);
            return;
        }
        if (!_nooks.TryGet(sp.NookId, out NookSession nook))
        {
            _logger?.SubscribeUnknownNook(sp.NookId);
            await WriteResponseAsync(conn, Fail(req.Id, "not_found", $"unknown nook {sp.NookId}"), cancellationToken).ConfigureAwait(false);
            return;
        }

        const ulong streamId = 1;
        long head = nook.Ring.Head;
        long tail = nook.Ring.Tail;
        var checkpoint = _nooks.GetTerminalCheckpoint(sp.NookId);
        bool useCheckpoint = checkpoint is not null && ((long)sp.SinceOffset < checkpoint.Offset || (sp.SinceOffset == 0 && checkpoint.Offset == 0));
        long baseOffset = useCheckpoint ? checkpoint!.Offset : Math.Clamp((long)sp.SinceOffset, tail, head);
        _logger?.SubscribeStarted(sp.NookId, baseOffset, head, tail);
        var subResult = new SubscribeResult(
            streamId,
            (ulong)baseOffset,
            ProtocolConstants.FlowWindow,
            (ulong)head,
            Convert.ToBase64String(Encoding.ASCII.GetBytes(useCheckpoint ? checkpoint!.ModeSupplement : nook.Reader.TerminalModePreamble)),
            useCheckpoint ? Convert.ToBase64String(checkpoint!.Data) : "",
            useCheckpoint ? checkpoint!.Cols : 0,
            useCheckpoint ? checkpoint!.Rows : 0);
        await WriteResponseAsync(conn, new ControlResponse(req.Id, true, ToElement(subResult, CoveJsonContext.Default.SubscribeResult)), cancellationToken).ConfigureAwait(false);

        var sink = new SocketByteStreamSink(stream);
        var sender = new PtyStreamSender(streamId, nook.Session.SessionId, nook.Ring, baseOffset, sink, sp.NookId, _logger, () => nook.Reader.TerminalModePreamble, () =>
        {
            var currentCheckpoint = _nooks.GetTerminalCheckpoint(sp.NookId);
            return currentCheckpoint is null
                ? null
                : new TerminalResyncSnapshot(currentCheckpoint.Offset, currentCheckpoint.Data, currentCheckpoint.Cols, currentCheckpoint.Rows, currentCheckpoint.ModeSupplement);
        });
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
                    nook.Signal.Set();
                }
            }
            catch (System.Exception ex)
            {
                _logger?.SubscribeCreditLoopClosed(sp.NookId, ex.Message);
            }
            finally
            {
                streamCts.Cancel();
                nook.Signal.Set();
            }
        });

        try
        {
            while (!streamCts.IsCancellationRequested)
            {
                Task wait = nook.Signal.WaitAsync();
                lock (gate)
                {
                    if (!childMarked && nook.Reader.HasCompleted)
                    {
                        sender.MarkChildExited(nook.Reader.ExitCode);
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
            _logger?.SubscribeEnded(sp.NookId, sender.Ended, sender.Faulted);
            streamCts.Cancel();
            try { await creditLoop.ConfigureAwait(false); } catch (System.Exception ex) { _logger?.SubscribeCreditLoopClosed(sp.NookId, ex.Message); }
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

    private string? GetFocusedNook()
    {
        return _layout?.FocusedNookId();
    }

    private void PersistActiveBay(string baysRoot, ILogger logger)
    {
        if (_layout is not { } layout)
            return;
        PersistBay(layout.ActiveBayId, baysRoot, logger);
    }

    private void MarkNookBayDirty(string nookId)
    {
        if (_layout is not { } layout)
            return;
        foreach (var bayId in layout.BayIds)
        {
            if (System.Linq.Enumerable.Contains(layout.LeafNookIds(bayId), nookId, StringComparer.Ordinal))
            {
                lock (_dirtyBaysLock)
                    _dirtyBays.Add(bayId);
                return;
            }
        }
    }

    private void FlushDirtyBays(string baysRoot, ILogger logger)
    {
        string[] ids;
        lock (_dirtyBaysLock)
        {
            if (_dirtyBays.Count == 0)
                return;
            ids = new string[_dirtyBays.Count];
            _dirtyBays.CopyTo(ids);
            _dirtyBays.Clear();
        }
        foreach (var id in ids)
            PersistBay(id, baysRoot, logger);
    }

    private void PersistBay(string bayId, string baysRoot, ILogger logger)
    {
        if (_layout is not { } layout || _nooks is not { } nooks)
            return;
        if (!System.Linq.Enumerable.Contains(layout.BayIds, bayId, StringComparer.Ordinal))
        {
            logger.LogWarning("bay persist skipped: {BayId} not present in layout", bayId);
            return;
        }
        try
        {
            var wsId = bayId;
            var actor = _bays?.Get(wsId);
            var name = actor?.State.Name ?? wsId;
            var dir = actor?.State.ProjectDir ?? nooks.ProjectDir ?? System.Environment.CurrentDirectory;
            var wsDir = System.IO.Path.Combine(baysRoot, wsId);
            var icon = actor?.State.Icon;
            var snap = layout.ToSnapshot(wsId, name, dir) with { IconKind = icon?.Kind, IconValue = icon?.Value };
            var leafIds = new System.Collections.Generic.HashSet<string>(layout.LeafNookIds(wsId), System.StringComparer.Ordinal);
            var descs = nooks.Descriptors()
                .Where(d => leafIds.Contains(d.NookId))
                .Select(d =>
                {
                    var sid = _sessions?.GetState(d.NookId)?.SessionId;
                    var yolo = _launcher?.GetOverrides(d.NookId)?.Yolo ?? d.Yolo;
                    return d with
                    {
                        SessionId = string.IsNullOrEmpty(sid) ? d.SessionId : sid,
                        Yolo = yolo,
                    };
                })
                .ToArray();
            Cove.Engine.Layout.BayPersistence.Save(snap, descs, wsDir);
        }
        catch (System.Exception ex) { logger.LogWarning(ex, "bay persist failed"); }
    }

    private void PersistAllScrollback(string baysRoot, ILogger logger)
    {
        if (_layout is not { } layout || _nooks is not { } reg)
            return;
        try
        {
            foreach (var wsId in layout.BayIds)
            {
                var wsDir = System.IO.Path.Combine(baysRoot, wsId);
                foreach (var nookId in layout.LeafNookIds(wsId))
                {
                    var state = reg.CaptureTerminalRestoreState(nookId);
                    if (state is not null)
                    {
                        Cove.Engine.Layout.BayPersistence.SaveTerminalRestoreState(nookId, state, wsDir);
                        continue;
                    }
                    var bytes = reg.SnapshotRing(nookId);
                    if (bytes.Length > 0)
                        Cove.Engine.Layout.BayPersistence.SaveScrollback(nookId, bytes, wsDir);
                }
            }
        }
        catch (System.Exception ex) { logger.LogWarning(ex, "scrollback snapshot failed"); }
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
        return uri.StartsWith("cove://commands/bay.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/shore.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/wing.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/collection.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/resident.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/worktree.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/bay-command.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/task.", System.StringComparison.Ordinal)
            || uri.StartsWith("cove://commands/run.", System.StringComparison.Ordinal)
            || uri == "cove://commands/activity.acknowledge";
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
            shore: () => "default",
            wing: () => null,
            bay: () => "default",
            otherNooks: () => _nooks?.List().Select(p => p.NookId).ToList() ?? new List<string>()));
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
                .Where(c => c.Lifecycle == Cove.Engine.Bays.RunCommandLifecycle.Running)
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
        var summary = Volatile.Read(ref _restorationSummary);
        if (summary is not null)
            BroadcastEvent("restore.summary", summary, Cove.Engine.Restart.RestorationSummaryJsonContext.Default.RestorationSummaryEvent);
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

    private sealed class RestoreSpawner : Cove.Engine.Restart.IRestoreSpawner
    {
        private readonly NookRegistry _nooks;
        private readonly string _bayDir;
        private readonly string _bayId;
        private readonly Cove.Engine.Agents.AgentMessageRouter? _agentRouter;
        private readonly Cove.Engine.Sessions.SessionResumeOrchestrator? _sessions;
        private readonly Cove.Engine.Hooks.HookEventRouter? _hookRouter;
        private readonly ILogger _logger;

        public RestoreSpawner(NookRegistry nooks, string bayDir, string bayId, Cove.Engine.Agents.AgentMessageRouter? agentRouter, Cove.Engine.Sessions.SessionResumeOrchestrator? sessions, Cove.Engine.Hooks.HookEventRouter? hookRouter, ILogger logger)
        {
            _nooks = nooks;
            _bayDir = bayDir;
            _bayId = bayId;
            _agentRouter = agentRouter;
            _sessions = sessions;
            _hookRouter = hookRouter;
            _logger = logger;
        }

        public void Respawn(Cove.Engine.Restart.RestorableNook nook, string command, string[] args, string cwd)
        {
            try
            {
                var state = Cove.Engine.Layout.BayPersistence.LoadTerminalRestoreState(nook.NookId, _bayDir, _logger);
                if (state is not null)
                    _nooks.RespawnAs(nook.NookId, command, args, cwd, nook.Cols, nook.Rows, state, nook.Adapter, nook.AgentName);
                else
                    _nooks.RespawnAs(nook.NookId, command, args, cwd, nook.Cols, nook.Rows, Cove.Engine.Layout.BayPersistence.LoadScrollback(nook.NookId, _bayDir), nook.Adapter, nook.AgentName);
                if (!string.IsNullOrEmpty(nook.Title))
                    _nooks.Rename(nook.NookId, nook.Title!);
                if (!string.IsNullOrEmpty(nook.Adapter))
                {
                    _agentRouter?.Register(nook.NookId, nook.Adapter!, nook.AgentName, _bayId);
                    _sessions?.Register(nook.NookId, nook.Adapter!, nook.SessionId);
                    _hookRouter?.Seed(nook.NookId, nook.Adapter!, nook.SessionId);
                }
            }
            catch (System.Exception ex) { _logger.LogWarning(ex, "respawn on restore failed for {NookId}", nook.NookId); }
        }
    }

    private sealed class DaemonNotificationBus : Cove.Engine.Hooks.INotificationBus
    {
        private readonly DaemonHost _host;

        public DaemonNotificationBus(DaemonHost host) => _host = host;

        public void BroadcastNeedsInputSignal(string nookId, string adapter)
            => _host.BroadcastEvent("needs-input.signal", new Cove.Protocol.NeedsInputSignalDto(nookId, adapter), Cove.Protocol.CoveJsonContext.Default.NeedsInputSignalDto);

        public void BroadcastDockBadge(string nookId, string adapter)
            => _host.BroadcastEvent("dock.badge", new Cove.Protocol.NeedsInputSignalDto(nookId, adapter), Cove.Protocol.CoveJsonContext.Default.NeedsInputSignalDto);

        public void ClearNeedsInputSignal(string nookId)
            => _host.BroadcastEvent("needs-input.clear", new Cove.Protocol.NeedsInputSignalDto(nookId, ""), Cove.Protocol.CoveJsonContext.Default.NeedsInputSignalDto);

        public void ClearDockBadge()
            => _host.BroadcastEvent("dock.badge.clear", new Cove.Protocol.NeedsInputSignalDto("", ""), Cove.Protocol.CoveJsonContext.Default.NeedsInputSignalDto);

        public void DeliverNotification(string id, string title, string body, string nookId)
            => _host.BroadcastEvent("notification.deliver", new Cove.Protocol.NotificationDeliverDto(id, title, body, nookId), Cove.Protocol.CoveJsonContext.Default.NotificationDeliverDto);
    }
}
