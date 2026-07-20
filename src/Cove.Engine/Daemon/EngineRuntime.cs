using Cove.Adapters;
using Cove.Engine.Activity;
using Cove.Engine.Adapters;
using Cove.Engine.Agents;
using Cove.Engine.Bays;
using Cove.Engine.Browser;
using Cove.Engine.Captures;
using Cove.Engine.Config;
using Cove.Engine.Diagnostics;
using Cove.Engine.Dictation;
using Cove.Engine.Feedback;
using Cove.Engine.Filesystem;
using Cove.Engine.Hooks;
using Cove.Engine.Keybindings;
using Cove.Engine.Knowledge;
using Cove.Engine.Launch;
using Cove.Engine.Layout;
using Cove.Engine.Lifecycle;
using Cove.Engine.Lsp;
using Cove.Engine.Nooks;
using Cove.Engine.Notifications;
using Cove.Engine.Protocol;
using Cove.Engine.Pty;
using Cove.Engine.Restart;
using Cove.Engine.Search;
using Cove.Engine.Sessions;
using Cove.Engine.Skills;
using Cove.Engine.Snapshots;
using Cove.Engine.Theming;
using Cove.Platform;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Daemon;

internal sealed class EngineRuntime : IAsyncDisposable
{
    private readonly DaemonPaths _paths;
    private readonly ILogger _logger;
    private readonly CancellationToken _shutdownToken;
    private readonly EngineRuntimeComponents _components;
    private int _wired;
    private int _initialized;
    private int _disposed;

    private EngineRuntime(
        DaemonPaths paths,
        ILogger logger,
        EngineEventRouter events,
        DateTimeOffset startedAtUtc,
        CancellationToken shutdownToken,
        EngineRuntimeComponents components)
    {
        _paths = paths;
        _logger = logger;
        Events = events;
        StartedAtUtc = startedAtUtc;
        _shutdownToken = shutdownToken;
        _components = components;
    }

    public string EngineVersion { get; } = CoveBuild.InformationalVersion;

    public string Channel => _paths.Channel;

    public DateTimeOffset StartedAtUtc { get; }

    public EngineEventRouter Events { get; }

    public NookRegistry Nooks => _components.Nooks;

    public LayoutService Layout => _components.Layout;

    public BayManager Bays => _components.Bays;

    public HookEventRouter HookRouter => _components.HookRouter;

    public AgentMessageRouter AgentRouter => _components.AgentRouter;

    public SessionResumeOrchestrator Sessions => _components.Sessions;

    public StateBus StateBus => _components.StateBus;

    public NookScopeStore NookScopes => _components.NookScopes;

    public NookStreamRouter Streams => _components.Streams;

    public PersistenceCoordinator Persistence => _components.Persistence;

    public static async Task<EngineRuntime> CreateAsync(
        DaemonPaths paths,
        ILogger logger,
        EngineEventRouter events,
        DateTimeOffset startedAtUtc,
        CancellationToken shutdownToken)
    {
        var dataDir = paths.DataDir.Root;
        var baysRoot = Path.Combine(dataDir, "bays");
        var dictation = DictationTranscriptionRuntime.CreateNative(
            Path.Combine(dataDir, "models"),
            events.BroadcastDictation,
            logger);
        var ptyHost = PtyHostFactory.Create(logger);
        var probedPath = LoginShellPath.Probe(logger);
        var cliPath = CliBinLink.LinkPath(dataDir);
        var spawnEnvironment = new SpawnEnvironment(
            probedPath,
            dataDir,
            cliPath,
            "default",
            paths.Channel);
        var shellDir = ShellIntegration.Install(dataDir);
        var nooks = new NookRegistry(
            ptyHost,
            logger,
            spawnEnvironment,
            shellDir);
        var layout = new LayoutService();
        var persistence = new PersistenceCoordinator(
            layout,
            nooks,
            baysRoot,
            logger);
        var bays = new BayManager(
            emit: persistence.HandleBayChange,
            logger: logger,
            layout: layout);
        var runCommands = new RunCommandService(
            new RunCommandStore(
                Path.Combine(dataDir, "run-commands"),
                logger),
            new PtyRunCommandSessionFactory(
                ptyHost,
                spawnEnvironment,
                shellDir,
                logger),
            logger: logger);
        var restoration = new RestorationService(
            dataDir,
            logger,
            emitProgress: progress => events.Broadcast(
                "restore.progress",
                progress,
                RestorationJsonContext.Default.RestoreProgressEvent));
        var snapshots = new SnapshotService(
            dataDir,
            Path.Combine(dataDir, "snapshots"),
            new ProcessGitRunner(),
            logger);
        var skills = new SkillsService(dataDir, logger: logger);
        var agents = new AgentDefinitionStore(
            Path.Combine(dataDir, "agents"),
            logger);
        var launchProfiles = new LaunchProfileStore(
            Path.Combine(dataDir, "launch-profiles"),
            logger);
        var adapterEnvironment = new AdapterEnvStore(
            Path.Combine(dataDir, "adapter-env"),
            logger);
        AdapterManifestStore? manifestStore = null;
        var environmentPropagation = new EnvPropagationService(
            adapterEnvironment,
            new NookRegistryEnvTarget(nooks),
            adapter => manifestStore?.Load(adapter)?.Binary,
            logger);
        var hookServer = new HookHttpServer(dataDir, logger);
        var hookRouter = new HookEventRouter(logger);
        var agentRouter = new AgentMessageRouter();
        var activity = new ActivityAggregate(hookRouter, agentRouter);
        var sessions = new SessionResumeOrchestrator(logger);
        var recentSessions = new RecentSessionStore(dataDir, logger);
        var lifecycle = new AgentLifecycleController(logger);
        manifestStore = new AdapterManifestStore(
            Path.Combine(dataDir, "adapters"),
            logger);
        AdapterUpdateCommands.Configure(
            HarnessUpdateChecker.CreateNpm(logger));
        var sessionService = new SessionService(
            new MethodRunner(logger: logger),
            logger: logger);
        var registryCachePath = Path.Combine(
            dataDir,
            "adapters",
            "registry.json");
        var repoRoot = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                ".."));
        var developmentRegistryPath = Path.Combine(
            repoRoot,
            "..",
            "atrium-adapters",
            "registry.json");
        IRegistryFetcher registryFetcher =
            File.Exists(developmentRegistryPath)
                ? new FileRegistryFetcher(
                    developmentRegistryPath,
                    logger)
                : new HttpRegistryFetcher(
                    RegistryConstants.RegistryContentsUrl,
                    logger);
        var registry = new RegistryService(
            registryCachePath,
            registryFetcher);
        var launchAdapterLookup =
            new LaunchAdapterLookup(manifestStore);
        var launchProcessAcquirer =
            new LaunchProcessAcquirer(
                new MethodRunner(logger: logger),
                new BinaryDiscoveryService(logger),
                probedPath,
                logger);
        var resumeProtocol = new AdapterResumeProtocol(
            manifestStore,
            new MethodRunner(logger: logger),
            logger,
            launchAdapterLookup,
            launchProcessAcquirer);
        var resumeService = new AgentResumeService(resumeProtocol);
        var launcher = new LaunchOrchestrator(
            new LaunchCommandComposer(),
            launchAdapterLookup,
            launchProcessAcquirer,
            new LauncherOptionsResolver(
                launchAdapterLookup,
                launchProcessAcquirer,
                new LauncherOptionsParser(),
                logger),
            new LaunchProfileLookup(launchProfiles),
            resumeService,
            new LauncherOverrideStore(
                Path.Combine(dataDir, "launcher-overrides"),
                logger),
            logger);
        var taskService = new Cove.Tasks.TaskService(dataDir, logger);
        await taskService.StartAsync();
        var knowledgeKernel = new KnowledgePersistenceKernel(
            dataDir,
            logger);
        knowledgeKernel.EnsureAllSchemas();
        var runRestoration =
            new Cove.Tasks.Restart.RunRestorationService(
                taskService,
                logger);
        var restoredRuns = runRestoration.RestoreOnStartup();
        if (restoredRuns.RestoredRuns.Count > 0)
            logger.TaskRunsRestored(restoredRuns.RestoredRuns.Count);
        var scheduler = new Cove.Tasks.Scheduler.TaskSchedulerEngine(
            taskService,
            new Cove.Tasks.Schedules.CronosCronExpander(logger),
            new Cove.Tasks.Scheduler.SystemClock(),
            logger);
        var schedulerLoop = scheduler.StartAsync(shutdownToken);
        var stateBus = new StateBus(dataDir, logger);
        var extensions = new ExtensionRegistry(manifestStore);
        extensions.Index();
        var nookScopes = new NookScopeStore(dataDir, logger);
        var noteSnapshots = new NoteSnapshotService(dataDir, logger);
        var noteFiles = new NoteFileStore(
            dataDir,
            logger,
            noteSnapshots,
            knowledgeKernel.NotesIndexDatabase);
        noteFiles.RebuildIndexFromDisk();
        var timeline = new TimelineStore(
            dataDir,
            logger,
            knowledgeKernel.TimelineDatabase);
        var blackboard = new BlackboardStore(
            dataDir,
            logger,
            database: knowledgeKernel.MemoryDatabase);
        var memory = new MemoryStore(
            dataDir,
            logger,
            knowledgeKernel.MemoryDatabase);
        var memoryRanker = new MemoryRanker(
            memory,
            dataDir,
            logger,
            knowledgeKernel.MemoryDatabase);
        var proposals = new ProposalStore(
            dataDir,
            logger,
            knowledgeKernel.MemoryDatabase);
        var consolidator = new MemoryConsolidator(
            memory,
            proposals,
            logger);
        var edits = new EditsIndex(
            dataDir,
            logger,
            database: knowledgeKernel.SessionIndexDatabase);
        var corpus = new SessionCorpusIndexer(
            dataDir,
            logger,
            knowledgeKernel.SessionIndexDatabase);
        var vaultSettings = new VaultSettingsStore(dataDir, logger);
        var library = new LibraryStore(dataDir, logger);
        library.EnsureSchema();
        var reviews = new ReviewStore(dataDir, logger);
        var attribution = new AttributionIndex(dataDir, logger);
        var reviewDispatcher = new ReviewDispatcher(logger);
        var omniChat = new OmniChatStore(
            Path.Combine(dataDir, "omni-chat"),
            logger);
        var browser = new BrowserNookManager();
        var config = new ConfigService(dataDir, logger);
        var captures = new CaptureStore(dataDir, logger);
        var lspUserEntries = config.GetLspServerEntries()
            .Select(entry => new LspConfigEntry(
                entry.Languages.ToArray(),
                entry.Command,
                entry.Args.ToArray()))
            .ToList();
        var lspService = new LspService(logger, lspUserEntries);
        var diagnosticsSection = config.GetDiagnosticsSection();
        var diagnosticsConfig = new DiagnosticsConfig(
            diagnosticsSection.Enabled,
            false,
            100,
            TimeSpan.FromMilliseconds(
                diagnosticsSection.FlushIntervalMs),
            diagnosticsSection.CaptureTerminalStats,
            diagnosticsSection.CaptureMemoryStats,
            diagnosticsSection.FlushIntervalMs);
        var diagnostics = new DiagnosticsHub(
            diagnosticsConfig,
            logger);
        var performanceBundles = new PerformanceBundleService(
            diagnostics,
            Path.Combine(dataDir, "perf-bundles"),
            logger);
        var platformFileSystem = SystemPlatformFileSystem.Instance;
        var directoryListing = new DirectoryListingService(
            platformFileSystem,
            logger);
        var gitSummary = new GitSummaryService(
            platformFileSystem,
            new SystemProcessRunner(),
            logger);
        var feedbackStore = new FeedbackStore(
            Path.Combine(dataDir, "feedback"),
            platformFileSystem,
            logger: logger);
        var performanceResults = new PerformanceResultStore(
            Path.Combine(dataDir, "cache", "perf"),
            platformFileSystem,
            logger: logger);
        var gitReadModel = new GitReadModel(
            new ProcessGitRunner(),
            logger);
        var searchService = new SearchService(logger);
        var keybindings = new KeybindingEngine();
        DefaultKeymap.RegisterAll(keybindings);
        var savedKeybindings = config.GetKeybindingsJson();
        if (!string.IsNullOrEmpty(savedKeybindings))
        {
            try
            {
                keybindings.LoadFromJson(savedKeybindings);
            }
            catch (Exception ex)
            {
                logger.ConfigParseFailed("keybindings", ex.Message);
            }
        }
        var themes = new ThemeService(dataDir);
        var configuredTheme = config.GetTheme();
        var activatedTheme = themes.SetActiveIfKnown(configuredTheme);
        if (activatedTheme is null)
        {
            logger.ConfiguredThemeUnavailable(configuredTheme);
        }
        else if (!string.Equals(
                     activatedTheme.Name,
                     configuredTheme,
                     StringComparison.Ordinal))
        {
            logger.ConfiguredThemeFallback(
                configuredTheme,
                activatedTheme.Name);
        }
        var browserAutomation = new BrowserAutomationBridge(
            command => events.Broadcast(
                "browser.automation.exec",
                command,
                CoveJsonContext.Default.BrowserAutomationExecEvent),
            logger);
        var nookTypes = NookTypeRegistry.CreateWithBuiltins();
        var notificationPolicy = new NotificationPolicyEngine(
            dataDir,
            logger);
        var needsInputSignaler = new NeedsInputSignaler(
            activity,
            new DaemonNotificationBus(events),
            layout.FocusedNookId,
            notificationPolicy);
        var hookMatrix = new HookEnvelopeMatrix();
        PopulateHookMatrix(hookMatrix, manifestStore);
        var hookInjector = new ContextInjector(
            hookMatrix,
            ParseAwareness(config.Get("context.awareness")),
            logger);
        hookServer.Injector = hookInjector;
        var adaptersRoot = Path.Combine(dataDir, "adapters");
        Directory.CreateDirectory(adaptersRoot);
        var adapterReloadWatcher = new AdapterReloadWatcher(
            adaptersRoot,
            logger: logger);
        var ambientAggregator = new AmbientContextAggregator();
        hookServer.Aggregator = ambientAggregator;
        var screenScanner = new ScreenStateScanner(
            nooks,
            hookRouter,
            adapter => manifestStore.Load(adapter),
            logger);
        var streams = new NookStreamRouter(paths, nooks, logger);
        persistence.Attach(
            bays,
            sessions,
            launcher,
            hookRouter);

        var components = new EngineRuntimeComponents
        {
            PtyHost = ptyHost,
            Nooks = nooks,
            Layout = layout,
            Bays = bays,
            RunCommands = runCommands,
            Restoration = restoration,
            Snapshots = snapshots,
            Skills = skills,
            Agents = agents,
            LaunchProfiles = launchProfiles,
            AdapterEnvironment = adapterEnvironment,
            EnvironmentPropagation = environmentPropagation,
            AdapterReloadWatcher = adapterReloadWatcher,
            ManifestStore = manifestStore,
            Registry = registry,
            HookInjector = hookInjector,
            HookServer = hookServer,
            HookRouter = hookRouter,
            AgentRouter = agentRouter,
            Activity = activity,
            NeedsInputSignaler = needsInputSignaler,
            NotificationPolicy = notificationPolicy,
            Sessions = sessions,
            RecentSessions = recentSessions,
            OmniChat = omniChat,
            NookScopes = nookScopes,
            StateBus = stateBus,
            Extensions = extensions,
            Lifecycle = lifecycle,
            Launcher = launcher,
            TaskService = taskService,
            ResumeProtocol = resumeProtocol,
            Scheduler = scheduler,
            SchedulerLoop = schedulerLoop,
            Timeline = timeline,
            Blackboard = blackboard,
            NoteFiles = noteFiles,
            NoteSnapshots = noteSnapshots,
            Memory = memory,
            MemoryRanker = memoryRanker,
            Proposals = proposals,
            Consolidator = consolidator,
            Edits = edits,
            Corpus = corpus,
            VaultSettings = vaultSettings,
            Library = library,
            Reviews = reviews,
            Attribution = attribution,
            ReviewDispatcher = reviewDispatcher,
            NookTypes = nookTypes,
            Browser = browser,
            Config = config,
            Captures = captures,
            Diagnostics = diagnostics,
            PerformanceBundles = performanceBundles,
            DirectoryListing = directoryListing,
            GitSummary = gitSummary,
            FeedbackStore = feedbackStore,
            PerformanceResults = performanceResults,
            Dictation = dictation,
            GitReadModel = gitReadModel,
            SearchService = searchService,
            Themes = themes,
            Keybindings = keybindings,
            BrowserAutomation = browserAutomation,
            LspService = lspService,
            SessionService = sessionService,
            Persistence = persistence,
            Streams = streams,
            ScreenScanner = screenScanner,
            AmbientAggregator = ambientAggregator,
            AdaptersRoot = adaptersRoot,
        };
        var runtime = new EngineRuntime(
            paths,
            logger,
            events,
            startedAtUtc,
            shutdownToken,
            components);
        try
        {
            runtime.Wire();
            adapterReloadWatcher.Start();
            await hookServer.StartAsync().ConfigureAwait(false);
            return runtime;
        }
        catch
        {
            await runtime.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task InitializeAsync(
        HandoffTransport handoff,
        HandoffTakeover? takeover)
    {
        var restoration = _components.Restoration;
        var savedState = restoration.LoadState();
        var wasClean = savedState.CleanShutdown;
        restoration.MarkLaunching();
        restoration.EmitProgress(
            "default",
            "load_bay",
            RestorePhase.Started,
            wasClean ? "clean" : "unclean");
        var loadedBays = BayStartup.Enumerate(
            _components.Persistence.BaysRoot,
            _logger);
        var fallbackProjectDirectory = Environment.CurrentDirectory;
        var restoreEnabled =
            _components.Config.GetSessionRestoreOnLaunch();
        ResumeCommand BuildResume(
            string adapter,
            string sessionId,
            LauncherOverrides overrides)
        {
            return _components.ResumeProtocol
                .BuildResumeCommandAsync(
                    adapter,
                    sessionId,
                    overrides)
                .GetAwaiter()
                .GetResult();
        }
        var adoptedIds = takeover is not null
            ? handoff.AdoptTakenOverNooks(takeover)
            : new HashSet<string>(StringComparer.Ordinal);
        var restoreTotals = new RestoreSummary(0, 0, 0);
        foreach (var entry in loadedBays)
        {
            var snapshot = entry.Snapshot;
            var restorables = new List<RestorableNook?>();
            foreach (var shore in snapshot.Shores)
            {
                foreach (var leaf in MosaicOps.Leaves(shore.LayoutTree))
                {
                    if (entry.Sessions.TryGetValue(
                            leaf.NookId,
                            out var descriptor)
                        && !adoptedIds.Contains(descriptor.NookId))
                    {
                        restorables.Add(
                            new RestorableNook(
                                descriptor.NookId,
                                descriptor.Command,
                                descriptor.Args,
                                descriptor.Cwd,
                                descriptor.Title,
                                descriptor.Adapter,
                                descriptor.AgentName,
                                descriptor.SessionId,
                                descriptor.Yolo,
                                descriptor.Cols,
                                descriptor.Rows));
                    }
                }
            }
            var spawner = new DaemonRestoreSpawner(
                _components.Nooks,
                entry.BayDir,
                snapshot.Id,
                _components.AgentRouter,
                _components.Sessions,
                _components.HookRouter,
                _logger);
            var restorer = new SessionRestorer(
                spawner,
                BuildResume,
                _logger);
            var summary = restorer.Restore(
                restorables,
                restoreEnabled);
            restoreTotals = new RestoreSummary(
                restoreTotals.Restored + summary.Restored,
                restoreTotals.Fresh + summary.Fresh,
                restoreTotals.Skipped + summary.Skipped);
            var displayName = BayStartup.DisplayName(
                snapshot,
                fallbackProjectDirectory);
            var projectDirectory =
                string.IsNullOrWhiteSpace(snapshot.ProjectDir)
                    ? fallbackProjectDirectory
                    : snapshot.ProjectDir;
            var icon = string.IsNullOrEmpty(snapshot.IconKind)
                ? null
                : new BayIcon(
                    snapshot.IconKind,
                    snapshot.IconValue ?? "");
            await _components.Bays.RestoreBayAsync(
                snapshot,
                displayName,
                projectDirectory,
                icon: icon).ConfigureAwait(false);
            _logger.BayStartupAdopted(
                snapshot.Id,
                displayName,
                snapshot.Shores.Count,
                projectDirectory);
        }
        if (loadedBays.Count == 0)
        {
            var seedDirectory =
                _components.Nooks.ProjectDir
                ?? fallbackProjectDirectory;
            var seedName = Path.GetFileName(
                seedDirectory.TrimEnd('/', '\\'));
            if (string.IsNullOrWhiteSpace(seedName))
                seedName = "Bay";
            var seeded = await _components.Bays.CreateBayAsync(
                seedName,
                seedDirectory).ConfigureAwait(false);
            _logger.BayStartupSeeded(
                seeded.Id,
                seedName,
                seedDirectory);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(savedState.FocusedBay))
                _components.Bays.RestoreActiveBay(savedState.FocusedBay);
            var focusedBayId = _components.Bays.ActiveBayId;
            var focusedBay = _components.Bays.Get(focusedBayId);
            if (focusedBay is not null
                && !string.IsNullOrEmpty(
                    focusedBay.State.ProjectDir))
            {
                _components.Nooks.ProjectDir =
                    focusedBay.State.ProjectDir;
            }
        }
        if (restoreTotals.Restored
            + restoreTotals.Fresh
            + restoreTotals.Skipped
            > 0)
        {
            Events.SetRestorationSummary(
                new RestorationSummaryEvent(
                    restoreTotals.Restored,
                    restoreTotals.Fresh,
                    restoreTotals.Skipped,
                    StartedAtUtc.ToString("o")));
            _logger.SessionRestorationCompleted(
                restoreTotals.Restored,
                restoreTotals.Fresh,
                restoreTotals.Skipped);
        }
        restoration.EmitProgress(
            "default",
            "materialize_nooks",
            RestorePhase.NooksMaterialized);
        try
        {
            await _components.RunCommands
                .RelaunchPreviouslyRunningAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.RunCommandRelaunchFailed(ex.Message);
        }
        restoration.EmitProgress(
            "default",
            "restore_complete",
            RestorePhase.Completed);
        PopulateAmbientAggregator();
        _components.Persistence.StartSnapshotLoop();
        _components.ScreenScanner.Start();
        Volatile.Write(ref _initialized, 1);
    }

    public async Task PublishReadyAsync()
    {
        CliBinLink.Ensure(
            _paths.DataDir.Root,
            Environment.ProcessPath,
            _logger);
        await _components.HookServer
            .PublishPortAsync()
            .ConfigureAwait(false);
        var report = BundledAdapterSeeder.SeedFromBinaryLocation(
            _components.AdaptersRoot,
            _logger);
        if (report.Copied.Count + report.Refreshed.Count > 0)
        {
            _logger.BundledAdaptersSeeded(
                report.Copied.Count,
                report.Refreshed.Count,
                report.SkippedUserManaged.Count);
            OnAdaptersChanged();
        }
    }

    public Task<ControlResponse?> RouteAsync(
        ControlRequest request,
        DaemonCommandSagas sagas,
        CancellationToken cancellationToken)
    {
        return EngineCommandRouter.RouteAsync(
            request,
            _components.Nooks,
            _components.Layout,
            _components.Bays,
            _components.RunCommands,
            _components.Restoration,
            _components.Snapshots,
            _components.Skills,
            _components.Agents,
            _components.LaunchProfiles,
            _components.AdapterEnvironment,
            _components.HookServer,
            _components.HookRouter,
            _components.AgentRouter,
            _components.Activity,
            _components.Sessions,
            _components.Lifecycle,
            _components.Launcher,
            _components.TaskService,
            sagas.Dispatch,
            sagas.Resume,
            _components.Timeline,
            _components.Blackboard,
            _components.NoteFiles,
            _components.Memory,
            _components.MemoryRanker,
            _components.Proposals,
            _components.Consolidator,
            _components.Edits,
            _components.Corpus,
            _components.VaultSettings,
            _components.Library,
            _components.Reviews,
            _components.Attribution,
            _components.ReviewDispatcher,
            _components.NookTypes,
            _components.Browser,
            _components.Config,
            _components.ManifestStore,
            _components.Registry,
            _components.OmniChat,
            _components.NookScopes,
            _components.StateBus,
            _components.Extensions,
            _components.Captures,
            _components.GitReadModel,
            _components.SearchService,
            _components.Themes,
            _components.Keybindings,
            _components.BrowserAutomation,
            _components.Diagnostics,
            _components.PerformanceBundles,
            _components.RecentSessions,
            _components.LspService,
            _components.SessionService,
            _components.Persistence.BaysRoot,
            _components.Scheduler,
            _components.DirectoryListing,
            _components.GitSummary,
            _components.FeedbackStore,
            _components.PerformanceResults,
            _components.Dictation,
            cancellationToken,
            Events.TryForwardFocus,
            () => Events.RestorationSummary,
            StartedAtUtc);
    }

    public Task ShutdownAsync()
    {
        if (Volatile.Read(ref _initialized) == 0)
            return Task.CompletedTask;
        try
        {
            _components.Restoration.MarkCleanShutdown();
        }
        catch (Exception ex)
        {
            _logger.CleanShutdownMarkerFailed(ex.Message);
        }
        try
        {
            _components.Persistence.FlushOnShutdown();
        }
        catch (Exception ex)
        {
            _logger.ShutdownPersistenceFlushFailed(ex.Message);
        }
        return Task.CompletedTask;
    }

    private void Wire()
    {
        if (Interlocked.Exchange(ref _wired, 1) != 0)
            throw new InvalidOperationException("engine runtime already wired");
        _components.Config.SettingsChanged += OnSettingsChanged;
        _components.HookServer.OnEvent +=
            _components.HookRouter.Route;
        _components.HookRouter.NeedsInputTransition +=
            OnNeedsInputTransition;
        _components.HookRouter.StateChanged +=
            OnHookStateChanged;
        _components.AdapterReloadWatcher.AdaptersChanged +=
            OnAdaptersChanged;
    }

    private void Unwire()
    {
        if (Interlocked.Exchange(ref _wired, 0) == 0)
            return;
        _components.Config.SettingsChanged -= OnSettingsChanged;
        _components.HookServer.OnEvent -=
            _components.HookRouter.Route;
        _components.HookRouter.NeedsInputTransition -=
            OnNeedsInputTransition;
        _components.HookRouter.StateChanged -=
            OnHookStateChanged;
        _components.AdapterReloadWatcher.AdaptersChanged -=
            OnAdaptersChanged;
    }

    private void OnSettingsChanged(string key)
    {
        Events.Broadcast(
            "config.changed",
            new ConfigChangedEvent(key),
            CoveJsonContext.Default.ConfigChangedEvent);
    }

    private void OnNeedsInputTransition(
        string nookId,
        bool needsInput)
    {
        if (needsInput)
            _components.NeedsInputSignaler.CheckAndSignal(nookId);
        else
            _components.NeedsInputSignaler.ClearSignal(nookId);
    }

    private void OnHookStateChanged(string nookId)
    {
        Events.Broadcast(
            "agent.changed",
            new AgentChangedEvent(nookId),
            CoveJsonContext.Default.AgentChangedEvent);
    }

    private void OnAdaptersChanged()
    {
        var matrix = new HookEnvelopeMatrix();
        PopulateHookMatrix(matrix, _components.ManifestStore);
        _components.HookInjector.SwapMatrix(matrix);
        Events.Broadcast(
            "state.changed",
            new StateChangedEvent(
                "cove://events/adapters.changed"),
            CoveJsonContext.Default.StateChangedEvent);
    }

    private void PopulateAmbientAggregator()
    {
        var primerPath = Path.Combine(
            _paths.DataDir.Root,
            "cove-context.md");
        _components.AmbientAggregator.Add(
            "sessionStartManifest",
            new SessionStartContextProvider(
                primer: () => File.Exists(primerPath)
                    ? File.ReadAllText(primerPath)
                    : "",
                skillsManifest: BuildSkillsManifest,
                agentPackaging: () => ""));
        _components.AmbientAggregator.Add(
            "userPromptSubmit",
            new LocationContextProvider(
                shore: () => "default",
                wing: () => null,
                bay: () => "default",
                otherNooks: () => _components.Nooks
                    .List()
                    .Select(nook => nook.NookId)
                    .ToList()));
        _components.AmbientAggregator.Add(
            "preToolUse",
            new RunCommandContextProvider(
                runningCommands: GetRunningCommands));
    }

    private string BuildSkillsManifest()
    {
        var entries = _components.Skills.List();
        if (entries.Count == 0)
            return "";
        using var buffer = new MemoryStream();
        using (var writer =
               new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var skill in entries)
            {
                writer.WriteStartObject();
                writer.WriteString("name", skill.Name);
                writer.WriteString(
                    "source",
                    skill.Source.ToString().ToLowerInvariant());
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.Flush();
        }
        return System.Text.Encoding.UTF8.GetString(
            buffer.ToArray());
    }

    private IReadOnlyList<string> GetRunningCommands()
    {
        try
        {
            return _components.RunCommands
                .ListEffectiveAsync("default", null)
                .GetAwaiter()
                .GetResult()
                .Where(command =>
                    command.Lifecycle
                    == RunCommandLifecycle.Running)
                .Select(command => command.Definition.Label)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.AmbientRunCommandFailed(ex.Message);
            return Array.Empty<string>();
        }
    }

    private static AwarenessLevel ParseAwareness(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "off" => AwarenessLevel.Off,
            "minimal" => AwarenessLevel.Minimal,
            _ => AwarenessLevel.Full,
        };
    }

    private static void PopulateHookMatrix(
        HookEnvelopeMatrix matrix,
        AdapterManifestStore manifestStore)
    {
        foreach (var manifest in manifestStore.LoadAll())
            matrix.RegisterFromManifest(manifest);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Unwire();
        _components.Scheduler.Stop();
        try
        {
            await _components.SchedulerLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.RuntimeComponentDisposeFailed(
                nameof(Cove.Tasks.Scheduler.TaskSchedulerEngine),
                exception.Message);
        }
        await TryDisposeAsync(
            _components.TaskService,
            nameof(Cove.Tasks.TaskService)).ConfigureAwait(false);
        await TryDisposeAsync(
            _components.Dictation,
            nameof(DictationTranscriptionRuntime)).ConfigureAwait(false);
        TryDispose(
            _components.ScreenScanner,
            nameof(ScreenStateScanner));
        TryDispose(
            _components.Persistence,
            nameof(PersistenceCoordinator));
        await TryDisposeAsync(
            _components.RunCommands,
            nameof(RunCommandService)).ConfigureAwait(false);
        await TryDisposeAsync(
            _components.Bays,
            nameof(BayManager)).ConfigureAwait(false);
        await TryDisposeAsync(
            _components.LspService,
            nameof(LspService)).ConfigureAwait(false);
        TryDispose(
            _components.Nooks,
            nameof(NookRegistry));
        TryDispose(
            _components.Skills,
            nameof(SkillsService));
        TryDispose(
            _components.AdapterReloadWatcher,
            nameof(AdapterReloadWatcher));
        TryDispose(
            _components.EnvironmentPropagation,
            nameof(EnvPropagationService));
        TryDispose(
            _components.HookServer,
            nameof(HookHttpServer));
    }

    private void TryDispose(IDisposable disposable, string component)
    {
        try
        {
            disposable.Dispose();
        }
        catch (Exception ex)
        {
            _logger.RuntimeComponentDisposeFailed(
                component,
                ex.Message);
        }
    }

    private async ValueTask TryDisposeAsync(
        IAsyncDisposable disposable,
        string component)
    {
        try
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.RuntimeComponentDisposeFailed(
                component,
                ex.Message);
        }
    }
}

internal sealed class EngineRuntimeComponents
{
    public required IPtyHost PtyHost { get; init; }
    public required NookRegistry Nooks { get; init; }
    public required LayoutService Layout { get; init; }
    public required BayManager Bays { get; init; }
    public required RunCommandService RunCommands { get; init; }
    public required RestorationService Restoration { get; init; }
    public required SnapshotService Snapshots { get; init; }
    public required SkillsService Skills { get; init; }
    public required AgentDefinitionStore Agents { get; init; }
    public required LaunchProfileStore LaunchProfiles { get; init; }
    public required AdapterEnvStore AdapterEnvironment { get; init; }
    public required EnvPropagationService EnvironmentPropagation { get; init; }
    public required AdapterReloadWatcher AdapterReloadWatcher { get; init; }
    public required AdapterManifestStore ManifestStore { get; init; }
    public required RegistryService Registry { get; init; }
    public required ContextInjector HookInjector { get; init; }
    public required HookHttpServer HookServer { get; init; }
    public required HookEventRouter HookRouter { get; init; }
    public required AgentMessageRouter AgentRouter { get; init; }
    public required ActivityAggregate Activity { get; init; }
    public required NeedsInputSignaler NeedsInputSignaler { get; init; }
    public required NotificationPolicyEngine NotificationPolicy { get; init; }
    public required SessionResumeOrchestrator Sessions { get; init; }
    public required RecentSessionStore RecentSessions { get; init; }
    public required OmniChatStore OmniChat { get; init; }
    public required NookScopeStore NookScopes { get; init; }
    public required StateBus StateBus { get; init; }
    public required ExtensionRegistry Extensions { get; init; }
    public required AgentLifecycleController Lifecycle { get; init; }
    public required LaunchOrchestrator Launcher { get; init; }
    public required Cove.Tasks.TaskService TaskService { get; init; }
    public required AdapterResumeProtocol ResumeProtocol { get; init; }
    public required Cove.Tasks.Scheduler.TaskSchedulerEngine Scheduler { get; init; }
    public required Task SchedulerLoop { get; init; }
    public required TimelineStore Timeline { get; init; }
    public required BlackboardStore Blackboard { get; init; }
    public required NoteFileStore NoteFiles { get; init; }
    public required NoteSnapshotService NoteSnapshots { get; init; }
    public required MemoryStore Memory { get; init; }
    public required MemoryRanker MemoryRanker { get; init; }
    public required ProposalStore Proposals { get; init; }
    public required MemoryConsolidator Consolidator { get; init; }
    public required EditsIndex Edits { get; init; }
    public required SessionCorpusIndexer Corpus { get; init; }
    public required VaultSettingsStore VaultSettings { get; init; }
    public required LibraryStore Library { get; init; }
    public required ReviewStore Reviews { get; init; }
    public required AttributionIndex Attribution { get; init; }
    public required ReviewDispatcher ReviewDispatcher { get; init; }
    public required NookTypeRegistry NookTypes { get; init; }
    public required BrowserNookManager Browser { get; init; }
    public required ConfigService Config { get; init; }
    public required CaptureStore Captures { get; init; }
    public required DiagnosticsHub Diagnostics { get; init; }
    public required PerformanceBundleService PerformanceBundles { get; init; }
    public required DirectoryListingService DirectoryListing { get; init; }
    public required GitSummaryService GitSummary { get; init; }
    public required FeedbackStore FeedbackStore { get; init; }
    public required PerformanceResultStore PerformanceResults { get; init; }
    public required DictationTranscriptionRuntime Dictation { get; init; }
    public required GitReadModel GitReadModel { get; init; }
    public required SearchService SearchService { get; init; }
    public required ThemeService Themes { get; init; }
    public required KeybindingEngine Keybindings { get; init; }
    public required BrowserAutomationBridge BrowserAutomation { get; init; }
    public required LspService LspService { get; init; }
    public required SessionService SessionService { get; init; }
    public required PersistenceCoordinator Persistence { get; init; }
    public required NookStreamRouter Streams { get; init; }
    public required ScreenStateScanner ScreenScanner { get; init; }
    public required AmbientContextAggregator AmbientAggregator { get; init; }
    public required string AdaptersRoot { get; init; }
}
