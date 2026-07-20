using Cove.Engine.Launch;
using Cove.Persistence;
using Cove.Platform;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Pty;

public sealed record HandoffExportItem(HandoffNookRecord Record, int MasterFd, byte[] RingTail);

public enum NookAuthResult { Bound, Unknown, Rejected }

public sealed class NookRegistry : IDisposable, Cove.Engine.Agents.INookWriter
{
    private readonly IPtyHost _host;
    private readonly ILogger _logger;
    private readonly SpawnEnvironment? _spawnEnvironment;
    private readonly HarnessStartupContext _startupContext;
    private readonly string? _shellDir;
    private readonly IShellResolver _shellResolver;
    private readonly NookIdentityService _identities = new();
    private readonly NookSessionOwner _sessions = new();
    private readonly NookTerminalState _terminalState;
    private readonly NookHandoffCoordinator _handoff;
    private string? _projectDir;

    public NookRegistry(
        IPtyHost host,
        ILogger logger,
        SpawnEnvironment? spawnEnv = null,
        string? shellDir = null,
        string? projectDir = null,
        IShellResolver? shellResolver = null,
        HarnessStartupContext? startupContext = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(logger);
        _host = host;
        _logger = logger;
        _spawnEnvironment = spawnEnv;
        _startupContext = startupContext ?? new HarnessStartupContext(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            logger);
        _shellDir = shellDir;
        _shellResolver = shellResolver ?? SystemShellResolver.Instance;
        _projectDir = projectDir;
        _terminalState = new NookTerminalState(_sessions, logger);
        _handoff = new NookHandoffCoordinator(host, _sessions, _terminalState, logger);
    }

    public string? ProjectDir
    {
        get => _projectDir;
        set => _projectDir = value;
    }

    public Action<string>? OnResized { get; set; }

    public NookInfo Spawn(SpawnParams parameters, string? defaultCwd = null)
    {
        NookIdentity identity;
        do
        {
            identity = _identities.Allocate();
        }
        while (_sessions.Contains(identity.NookId));

        var inherited = !string.IsNullOrEmpty(parameters.InheritCwdFrom)
            && _sessions.TryGet(parameters.InheritCwdFrom, out var source)
                ? source.Cwd
                : null;
        var fallback = !string.IsNullOrEmpty(defaultCwd) ? defaultCwd : _projectDir;
        if (string.IsNullOrEmpty(inherited)
            && string.IsNullOrEmpty(parameters.Cwd)
            && string.IsNullOrEmpty(fallback))
        {
            _logger.NookSpawnCwdFallback(
                identity.NookId,
                parameters.Adapter ?? "",
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        var cwd = ResolveWorkingDirectory(inherited, parameters.Cwd, fallback);
        var command = parameters.Command;
        var args = parameters.Args ?? [];
        if (!string.IsNullOrEmpty(parameters.ShellCommand))
        {
            var invocation = ShellInvocation.Create(
                _shellResolver.ResolveDefaultShell(),
                parameters.ShellCommand);
            command = invocation.Command;
            args = invocation.Args;
        }
        else if (string.IsNullOrWhiteSpace(command))
        {
            command = _shellResolver.ResolveDefaultShell();
        }

        _logger.NookSpawn(
            identity.NookId,
            command,
            parameters.Adapter ?? "",
            parameters.Yolo,
            !string.IsNullOrEmpty(parameters.SessionId),
            parameters.Cols,
            parameters.Rows);
        var info = SpawnCore(
            identity,
            command,
            args,
            cwd,
            parameters.Cols,
            parameters.Rows,
            parameters.Env,
            parameters.Adapter);
        _sessions.Tag(identity.NookId, parameters.Adapter, parameters.AgentName);
        return info;
    }

    public NookInfo RespawnAs(
        string nookId,
        string command,
        string[] args,
        string cwd,
        int cols,
        int rows,
        byte[]? priorScrollback = null,
        string? adapter = null,
        string? agentName = null,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        _logger.NookRespawn(nookId, command, adapter ?? "");
        var info = SpawnCore(
            _identities.Issue(nookId),
            command,
            args,
            cwd,
            cols,
            rows,
            environment,
            adapter,
            priorScrollback);
        _sessions.Tag(nookId, adapter, agentName);
        return info;
    }

    public NookInfo RespawnAs(
        string nookId,
        string command,
        string[] args,
        string cwd,
        int cols,
        int rows,
        TerminalRestoreState restoreState,
        string? adapter = null,
        string? agentName = null,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        _logger.NookRespawn(nookId, command, adapter ?? "");
        var info = SpawnCore(
            _identities.Issue(nookId),
            command,
            args,
            cwd,
            cols,
            rows,
            environment,
            adapter,
            restoreState.Tail);
        if (_sessions.TryGet(nookId, out var session))
            _terminalState.RestoreCheckpoint(session, restoreState);
        _sessions.Tag(nookId, adapter, agentName);
        return info;
    }

    private NookInfo SpawnCore(
        NookIdentity identity,
        string command,
        string[] args,
        string cwd,
        int cols,
        int rows,
        IReadOnlyDictionary<string, string>? callerEnvironment,
        string? adapter,
        byte[]? priorScrollback = null)
    {
        var environment = _spawnEnvironment is { } spawnEnvironment
            ? spawnEnvironment.Build(identity.NookId, callerEnvironment, identity.Token)
            : callerEnvironment;
        if (environment is Dictionary<string, string> managedEnvironment
            && !string.IsNullOrWhiteSpace(adapter))
        {
            var startupCommand = _startupContext.Apply(
                adapter,
                command,
                args,
                managedEnvironment);
            command = startupCommand.Command;
            args = startupCommand.Args;
        }
        if (environment is Dictionary<string, string> environmentDictionary
            && _shellDir is { } shellDir)
        {
            args = [.. ShellIntegration.Apply(command, shellDir, args, environmentDictionary)];
        }

        var request = new PtySpawnRequest
        {
            Command = command,
            Args = args,
            WorkingDirectory = cwd,
            Environment = environment,
            Cols = cols,
            Rows = rows,
        };
        var ptySession = _host.Spawn(request);
        _logger.NookSpawnEnv(identity.NookId, environment?.Count ?? 0, args.Length, cwd);
        var ring = new PtyRingBuffer();
        if (priorScrollback is { Length: > 0 })
            ring.Append(priorScrollback);
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(
            ptySession,
            ring,
            signal,
            _logger,
            identity.NookId);
        var session = new NookSession(
            identity.NookId,
            command,
            args,
            cwd,
            cols,
            rows,
            ptySession,
            ring,
            signal,
            reader,
            identity.Token,
            _logger);
        reader.OnCwd = currentCwd => session.Cwd = currentCwd;
        _sessions.Replace(session);
        reader.Start();
        return session.ToInfo();
    }

    public NookDescriptor[] Descriptors() => _sessions.Descriptors();

    public static string ResolveWorkingDirectory(
        string? inheritedCwd,
        string? explicitCwd,
        string? projectDir = null) =>
        !string.IsNullOrEmpty(inheritedCwd)
            ? inheritedCwd
            : !string.IsNullOrEmpty(explicitCwd)
                ? explicitCwd
                : !string.IsNullOrEmpty(projectDir)
                    ? projectDir
                    : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public NookAuthResult Authenticate(string nookId, string? token)
    {
        if (!_sessions.TryGet(nookId, out var session))
            return NookAuthResult.Unknown;
        return _identities.Authenticate(session.Token, token);
    }

    public Cove.Engine.Protocol.PrefixResolveResult ResolveId(string idOrPrefix)
    {
        var resolver = new Cove.Engine.Protocol.PrefixResolver();
        foreach (var nookId in _sessions.NookIds())
            resolver.Index("nook", nookId);
        return resolver.Resolve("nook", idOrPrefix);
    }

    public NookInfo[] List() => _sessions.List();

    public List<(string NookId, string Adapter)> ListAdapterNooks() =>
        _sessions.ListAdapterNooks();

    public bool ConsumePendingRepaint(string nookId) =>
        _terminalState.ConsumePendingRepaint(nookId);

    public (long Head, byte[] Delta)? TryGetScreenSample(
        string nookId,
        long sinceOffset,
        int maxBytes) =>
        _terminalState.TryGetScreenSample(nookId, sinceOffset, maxBytes);

    public bool Write(string nookId, ReadOnlySpan<byte> data)
    {
        if (!_sessions.TryGet(nookId, out var session))
        {
            _logger.NookWriteUnknown(nookId);
            return false;
        }
        _logger.NookWrite(nookId, data.Length);
        session.Session.Write(data);
        return true;
    }

    public bool Resize(string nookId, int cols, int rows)
    {
        if (!_sessions.TryGet(nookId, out var session))
        {
            _logger.NookResizeUnknown(nookId);
            return false;
        }
        session.Session.Resize(cols, rows);
        session.Cols = cols;
        session.Rows = rows;
        OnResized?.Invoke(nookId);
        return true;
    }

    public bool Kill(string nookId)
    {
        if (!_sessions.Terminate(nookId))
        {
            _logger.NookKillUnknown(nookId);
            return false;
        }
        _logger.NookKill(nookId);
        return true;
    }

    public IReadOnlyList<HandoffExportItem> ExportForHandoff() => _handoff.Export();

    public NookInfo? Adopt(HandoffNookRecord record, int masterFd, byte[] ringTail) =>
        _handoff.Adopt(record, masterFd, ringTail);

    public bool Stop(string nookId)
    {
        if (!_sessions.TryGet(nookId, out var session))
        {
            _logger.NookKillUnknown(nookId);
            return false;
        }
        try
        {
            return session.Session.Signal(PtyConstants.SigTerm);
        }
        catch (Exception ex)
        {
            _logger.NookStopFailed(nookId, ex.Message);
            return false;
        }
    }

    internal bool Signal(string nookId, int signal)
    {
        if (!_sessions.TryGet(nookId, out var session))
            return false;
        try
        {
            return session.Session.Signal(signal);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public SearchMatch[] Search(string nookId, string query, bool caseSensitive) =>
        _terminalState.Search(nookId, query, caseSensitive);

    public byte[] SnapshotRing(string nookId) => _terminalState.SnapshotRing(nookId);

    public bool StoreTerminalCheckpoint(
        string nookId,
        byte[] checkpoint,
        long offset,
        int cols,
        int rows,
        int scrollbackLines) =>
        _terminalState.StoreTerminalCheckpoint(
            nookId,
            checkpoint,
            offset,
            cols,
            rows,
            scrollbackLines);

    public bool StoreTerminalCheckpointBase64(
        string nookId,
        string checkpointBase64,
        long offset,
        int cols,
        int rows,
        int scrollbackLines) =>
        _terminalState.StoreTerminalCheckpointBase64(
            nookId,
            checkpointBase64,
            offset,
            cols,
            rows,
            scrollbackLines);

    internal TerminalCheckpoint? GetTerminalCheckpoint(string nookId) =>
        _terminalState.GetTerminalCheckpoint(nookId);

    public TerminalRestoreState? CaptureTerminalRestoreState(string nookId) =>
        _terminalState.CaptureTerminalRestoreState(nookId);

    public bool Rename(string nookId, string title) => _sessions.Rename(nookId, title);

    public byte[] Read(string nookId, long offset, int maxBytes) =>
        _terminalState.Read(nookId, offset, maxBytes);

    internal long GetHead(string nookId) => _terminalState.TryGetHead(nookId);

    internal bool TryGetStreamState(string nookId, out NookStreamState state) =>
        _terminalState.TryGetStreamState(nookId, out state);

    internal bool Contains(string nookId) => _sessions.Contains(nookId);

    public void Dispose() => _sessions.Dispose();
}
