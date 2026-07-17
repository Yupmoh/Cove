using System;
using System.Collections.Generic;
using Cove.Persistence;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Pty;

internal sealed record TerminalCheckpoint(byte[] Data, long Offset, int Cols, int Rows, int ScrollbackLines, string ModeSupplement);

public sealed record HandoffExportItem(HandoffNookRecord Record, int MasterFd, byte[] RingTail);

internal sealed class NookSession
{
    public required string NookId { get; init; }
    public required string Command { get; init; }
    public required string[] Args { get; init; }
    public required string SpawnCwd { get; init; }
    public int Cols { get; set; }
    public int Rows { get; set; }
    public string? Cwd { get; set; }
    public string? Title { get; set; }
    public string? Adapter { get; set; }
    public string? AgentName { get; set; }
    public required IPtySession Session { get; init; }
    public required PtyRingBuffer Ring { get; init; }
    public required PtyRingSignal Signal { get; init; }
    public required PtySessionReader Reader { get; init; }
    public TerminalCheckpoint? Checkpoint { get; set; }
    public bool PendingRepaint { get; set; }

    public NookInfo ToInfo() => new(NookId, Command, Cols, Rows, !Reader.HasCompleted, Cwd, Title);
}

public sealed class NookRegistry : IDisposable, Cove.Engine.Agents.INookWriter
{
    private readonly IPtyHost _host;
    private readonly ILogger _logger;
    private readonly SpawnEnvironment? _spawnEnv;
    private readonly string? _shellDir;
    private string? _projectDir;
    private readonly object _sync = new();
    private readonly Dictionary<string, NookSession> _nooks = new();

    public NookRegistry(IPtyHost host, ILogger logger, SpawnEnvironment? spawnEnv = null, string? shellDir = null, string? projectDir = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(logger);
        _host = host;
        _logger = logger;
        _spawnEnv = spawnEnv;
        _shellDir = shellDir;
        _projectDir = projectDir;
    }

    public string? ProjectDir { get => _projectDir; set => _projectDir = value; }
    public Action<string>? OnResized { get; set; }
    public NookInfo Spawn(SpawnParams p, string? defaultCwd = null)
    {
        string nookId = "nook-" + System.Guid.NewGuid().ToString("N");
        string? inherited = (!string.IsNullOrEmpty(p.InheritCwdFrom) && TryGet(p.InheritCwdFrom!, out var src)) ? src.Cwd : null;
        string? fallback = !string.IsNullOrEmpty(defaultCwd) ? defaultCwd : _projectDir;
        if (string.IsNullOrEmpty(inherited) && string.IsNullOrEmpty(p.Cwd) && string.IsNullOrEmpty(fallback))
            _logger.NookSpawnCwdFallback(nookId, p.Adapter ?? "", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        string cwd = ResolveWorkingDirectory(inherited, p.Cwd, fallback);
        _logger.NookSpawn(nookId, p.Command, p.Adapter ?? "", p.Yolo, !string.IsNullOrEmpty(p.SessionId), p.Cols, p.Rows);
        var info = SpawnCore(nookId, p.Command, p.Args ?? System.Array.Empty<string>(), cwd, p.Cols, p.Rows, p.Env);
        Tag(nookId, p.Adapter, p.AgentName);
        return info;
    }

    public NookInfo RespawnAs(string nookId, string command, string[] args, string cwd, int cols, int rows, byte[]? priorScrollback = null, string? adapter = null, string? agentName = null)
    {
        _logger.NookRespawn(nookId, command, adapter ?? "");
        var info = SpawnCore(nookId, command, args, cwd, cols, rows, null, priorScrollback);
        Tag(nookId, adapter, agentName);
        return info;
    }
    public NookInfo RespawnAs(string nookId, string command, string[] args, string cwd, int cols, int rows, TerminalRestoreState restoreState, string? adapter = null, string? agentName = null)
    {
        _logger.NookRespawn(nookId, command, adapter ?? "");
        var info = SpawnCore(nookId, command, args, cwd, cols, rows, null, restoreState.Tail);
        lock (_sync)
        {
            if (_nooks.TryGetValue(nookId, out var nook))
                nook.Checkpoint = new TerminalCheckpoint(restoreState.Checkpoint, 0, restoreState.Cols, restoreState.Rows, restoreState.ScrollbackLines, restoreState.ModeSupplement);
        }
        Tag(nookId, adapter, agentName);
        return info;
    }


    private void Tag(string nookId, string? adapter, string? agentName)
    {
        if (string.IsNullOrEmpty(adapter))
            return;
        lock (_sync)
        {
            if (_nooks.TryGetValue(nookId, out var nook))
            {
                nook.Adapter = adapter;
                nook.AgentName = agentName;
            }
        }
    }

    private NookInfo SpawnCore(string nookId, string command, string[] args, string cwd, int cols, int rows, System.Collections.Generic.IReadOnlyDictionary<string, string>? callerEnv, byte[]? priorScrollback = null)
    {
        var envDict = _spawnEnv is { } se ? se.Build(nookId, callerEnv) : callerEnv;
        if (envDict is Dictionary<string, string> ed && _shellDir is { } sd)
            args = (string[])System.Linq.Enumerable.ToArray(ShellIntegration.Apply(command, sd, args, ed));
        var request = new PtySpawnRequest
        {
            Command = command,
            Args = args,
            WorkingDirectory = cwd,
            Environment = envDict,
            Cols = cols,
            Rows = rows,
        };
        IPtySession session = _host.Spawn(request);
        _logger.NookSpawnEnv(nookId, envDict?.Count ?? 0, args.Length, cwd);
        var ring = new PtyRingBuffer();
        if (priorScrollback is { Length: > 0 })
            ring.Append(priorScrollback);
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(session, ring, signal, _logger, nookId);
        var nook = new NookSession
        {
            NookId = nookId,
            Command = command,
            Args = args,
            SpawnCwd = cwd,
            Cols = cols,
            Rows = rows,
            Session = session,
            Ring = ring,
            Signal = signal,
            Reader = reader,
        };
        reader.OnCwd = c => nook.Cwd = c;
        reader.Start();
        lock (_sync)
            _nooks[nookId] = nook;
        return nook.ToInfo();
    }

    public NookDescriptor[] Descriptors()
    {
        lock (_sync)
        {
            var arr = new NookDescriptor[_nooks.Count];
            int i = 0;
            foreach (var p in _nooks.Values)
                arr[i++] = new NookDescriptor(p.NookId, p.Command, p.Args, string.IsNullOrEmpty(p.Cwd) ? p.SpawnCwd : p.Cwd!, p.Title, p.Adapter, p.AgentName, null, false, p.Cols, p.Rows);
            return arr;
        }
    }
    public static string ResolveWorkingDirectory(string? inheritedCwd, string? explicitCwd, string? projectDir = null) =>
        !string.IsNullOrEmpty(inheritedCwd) ? inheritedCwd! : (!string.IsNullOrEmpty(explicitCwd) ? explicitCwd! : (!string.IsNullOrEmpty(projectDir) ? projectDir! : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

    internal bool TryGet(string nookId, out NookSession nook)
    {
        lock (_sync)
            return _nooks.TryGetValue(nookId, out nook!);
    }

    public Cove.Engine.Protocol.PrefixResolveResult ResolveId(string idOrPrefix)
    {
        var resolver = new Cove.Engine.Protocol.PrefixResolver();
        lock (_sync)
            foreach (var id in _nooks.Keys)
                resolver.Index("nook", id);
        return resolver.Resolve("nook", idOrPrefix);
    }

    public NookInfo[] List()
    {
        lock (_sync)
        {
            var arr = new NookInfo[_nooks.Count];
            int i = 0;
            foreach (NookSession p in _nooks.Values)
                arr[i++] = p.ToInfo();
            return arr;
        }
    }

    public List<(string NookId, string Adapter)> ListAdapterNooks()
    {
        lock (_sync)
        {
            var result = new List<(string, string)>();
            foreach (NookSession nook in _nooks.Values)
                if (!string.IsNullOrEmpty(nook.Adapter))
                    result.Add((nook.NookId, nook.Adapter!));
            return result;
        }
    }

    public bool ConsumePendingRepaint(string nookId)
    {
        lock (_sync)
        {
            if (_nooks.TryGetValue(nookId, out var nook) && nook.PendingRepaint)
            {
                nook.PendingRepaint = false;
                return true;
            }
            return false;
        }
    }

    public (long Head, byte[] Delta)? TryGetScreenSample(string nookId, long sinceOffset, int maxBytes)
    {
        NookSession? nook;
        lock (_sync)
            _nooks.TryGetValue(nookId, out nook);
        if (nook is null || nook.Reader.HasCompleted)
            return null;
        var head = nook.Ring.Head;
        var from = Math.Max(nook.Ring.Tail, Math.Max(sinceOffset, head - maxBytes));
        if (from >= head)
            return (head, System.Array.Empty<byte>());
        if (!nook.Ring.TrySnapshotFrom(from, out var delta))
            return null;
        return (head, delta);
    }

    public bool Write(string nookId, ReadOnlySpan<byte> data)
    {
        if (!TryGet(nookId, out NookSession nook))
        {
            _logger.NookWriteUnknown(nookId);
            return false;
        }
        _logger.NookWrite(nookId, data.Length);
        nook.Session.Write(data);
        return true;
    }

    public bool Resize(string nookId, int cols, int rows)
    {
        if (!TryGet(nookId, out NookSession nook))
        {
            _logger.NookResizeUnknown(nookId);
            return false;
        }
        nook.Session.Resize(cols, rows);
        nook.Cols = cols;
        nook.Rows = rows;
        OnResized?.Invoke(nookId);
        return true;
    }

    public bool Kill(string nookId)
    {
        NookSession? nook;
        lock (_sync)
        {
            if (!_nooks.TryGetValue(nookId, out nook))
            {
                _logger.NookKillUnknown(nookId);
                return false;
            }
            _nooks.Remove(nookId);
        }
        _logger.NookKill(nookId);
        Terminate(nook, _logger);
        return true;
    }

    public IReadOnlyList<HandoffExportItem> ExportForHandoff()
    {
        List<NookSession> sessions;
        lock (_sync)
            sessions = new List<NookSession>(_nooks.Values);
        var items = new List<HandoffExportItem>(sessions.Count);
        foreach (var nook in sessions)
        {
            if (nook.Reader.HasCompleted)
            {
                _logger.HandoffSkipExited(nook.NookId);
                continue;
            }
            if (!_host.TryExportSession(nook.Session, out var fd, out var pid))
            {
                _logger.HandoffExportUnsupported(nook.NookId);
                continue;
            }
            int transferFd;
            try
            {
                transferFd = Cove.Platform.Pty.Unix.UnixFd.Duplicate(fd);
            }
            catch (Cove.Platform.Pty.PtyIoException ex)
            {
                _logger.HandoffAdoptRejected(nook.NookId, $"export dup failed: {ex.Message}");
                continue;
            }
            nook.Reader.DetachForHandoff();
            var tail = nook.Ring.Snapshot();
            var checkpoint = nook.Checkpoint is { } c
                ? new HandoffCheckpointDto(Convert.ToBase64String(c.Data), c.Offset, c.Cols, c.Rows, c.ScrollbackLines, c.ModeSupplement)
                : null;
            var record = new HandoffNookRecord(
                nook.NookId, pid, nook.Command, nook.Args, nook.SpawnCwd, nook.Cwd, nook.Cols, nook.Rows,
                nook.Title, nook.Adapter, nook.AgentName, nook.Ring.Head, tail.Length, null, null, checkpoint);
            _logger.HandoffExported(nook.NookId, pid, tail.Length);
            items.Add(new HandoffExportItem(record, transferFd, tail));
            lock (_sync)
                _nooks.Remove(nook.NookId);
        }
        return items;
    }

    public NookInfo? Adopt(HandoffNookRecord record, int masterFd, byte[] ringTail)
    {
        if (record.Pid <= 0 || masterFd < 0)
        {
            _logger.HandoffAdoptRejected(record.NookId, $"invalid transfer fd={masterFd} pid={record.Pid}");
            return null;
        }
        if (OperatingSystem.IsWindows() is false && Cove.Platform.Pty.Unix.ProcessExitWatch.WaitForExitAsync(record.Pid).IsCompleted)
        {
            _logger.HandoffAdoptRejected(record.NookId, $"pid {record.Pid} already exited");
            return null;
        }
        IPtySession session;
        try
        {
            session = _host.AdoptSession(masterFd, record.Pid);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or ArgumentOutOfRangeException)
        {
            _logger.HandoffAdoptRejected(record.NookId, ex.Message);
            return null;
        }
        var ring = new PtyRingBuffer();
        try
        {
            ring.RestoreAt(record.RingHead, ringTail);
        }
        catch (ArgumentException ex)
        {
            _logger.HandoffAdoptRejected(record.NookId, ex.Message);
            session.Dispose();
            return null;
        }
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(session, ring, signal, _logger, record.NookId);
        var nook = new NookSession
        {
            NookId = record.NookId,
            Command = record.Command,
            Args = record.Args,
            SpawnCwd = record.SpawnCwd,
            Cols = record.Cols,
            Rows = record.Rows,
            Session = session,
            Ring = ring,
            Signal = signal,
            Reader = reader,
        };
        nook.Cwd = record.Cwd;
        nook.PendingRepaint = true;
        nook.Title = record.Title;
        nook.Adapter = record.Adapter;
        nook.AgentName = record.AgentName;
        if (record.Checkpoint is { } cp)
            nook.Checkpoint = new TerminalCheckpoint(Convert.FromBase64String(cp.DataBase64), cp.Offset, cp.Cols, cp.Rows, cp.ScrollbackLines, cp.ModeSupplement);
        reader.OnCwd = c => nook.Cwd = c;
        try
        {
            session.Resize(record.Cols, record.Rows);
        }
        catch (PtyIoException ex)
        {
            _logger.HandoffAdoptRejected(record.NookId, "resize replay failed: " + ex.Message);
        }
        reader.Start();
        lock (_sync)
            _nooks[record.NookId] = nook;
        _logger.HandoffAdopted(record.NookId, record.Pid, record.RingHead);
        return nook.ToInfo();
    }

    public bool Stop(string nookId)
    {
        lock (_sync)
        {
            if (!_nooks.TryGetValue(nookId, out var nook))
            {
                _logger.NookKillUnknown(nookId);
                return false;
            }
            try { return nook.Session.Signal(Cove.Platform.Pty.PtyConstants.SigTerm); }
            catch (System.Exception ex) { _logger.NookStopFailed(nookId, ex.Message); return false; }
        }
    }

    public SearchMatch[] Search(string nookId, string query, bool caseSensitive)
    {
        if (!TryGet(nookId, out var nook))
            return Array.Empty<SearchMatch>();
        long tail = nook.Ring.Tail;
        long head = nook.Ring.Head;
        int len = (int)Math.Min(nook.Ring.Capacity, head - tail);
        if (len <= 0)
            return Array.Empty<SearchMatch>();
        var buf = new byte[len];
        var res = nook.Ring.ReadInto(tail, buf);
        return RingSearch.Find(buf.AsSpan(0, res.BytesCopied), query, caseSensitive);
    }

    public byte[] SnapshotRing(string nookId)
    {
        if (!TryGet(nookId, out var nook))
            return System.Array.Empty<byte>();
        return nook.Ring.Snapshot();
    }

    public bool StoreTerminalCheckpoint(string nookId, byte[] checkpoint, long offset, int cols, int rows, int scrollbackLines)
    {
        if (!TryGet(nookId, out var nook))
        {
            _logger.TerminalCheckpointUnknownNook(nookId);
            return false;
        }
        if (checkpoint.Length == 0 || offset < 0 || cols < 1 || rows < 1 || scrollbackLines < 0 || !nook.Ring.ContainsOffset(offset))
        {
            _logger.TerminalCheckpointRejected(nookId, offset, nook.Ring.Tail, nook.Ring.Head, cols, rows, checkpoint.Length);
            return false;
        }
        lock (_sync)
            nook.Checkpoint = new TerminalCheckpoint(checkpoint, offset, cols, rows, scrollbackLines, nook.Reader.TerminalCheckpointModeSupplement);
        return true;
    }

    public bool StoreTerminalCheckpointBase64(string nookId, string checkpointBase64, long offset, int cols, int rows, int scrollbackLines)
    {
        try
        {
            return StoreTerminalCheckpoint(nookId, System.Convert.FromBase64String(checkpointBase64), offset, cols, rows, scrollbackLines);
        }
        catch (System.FormatException ex)
        {
            _logger.TerminalCheckpointDecodeFailed(nookId, ex.Message);
            return false;
        }
    }

    internal TerminalCheckpoint? GetTerminalCheckpoint(string nookId)
    {
        if (!TryGet(nookId, out var nook))
        {
            _logger.TerminalCheckpointUnknownNook(nookId);
            return null;
        }
        TerminalCheckpoint? checkpoint;
        lock (_sync)
            checkpoint = nook.Checkpoint;
        if (checkpoint is not null && !nook.Ring.ContainsOffset(checkpoint.Offset))
        {
            _logger.TerminalCheckpointExpired(nookId, checkpoint.Offset, nook.Ring.Tail, nook.Ring.Head);
            return null;
        }
        return checkpoint;
    }

    public TerminalRestoreState? CaptureTerminalRestoreState(string nookId)
    {
        var checkpoint = GetTerminalCheckpoint(nookId);
        if (checkpoint is null || !TryGet(nookId, out var nook))
            return null;
        if (!nook.Ring.TrySnapshotFrom(checkpoint.Offset, out var tail))
        {
            _logger.TerminalCheckpointExpired(nookId, checkpoint.Offset, nook.Ring.Tail, nook.Ring.Head);
            return null;
        }
        return new TerminalRestoreState(checkpoint.Data, tail, checkpoint.Offset, checkpoint.Cols, checkpoint.Rows, checkpoint.ScrollbackLines, checkpoint.ModeSupplement);
    }

    public bool Rename(string nookId, string title)
    {
        lock (_sync)
        {
            if (!_nooks.TryGetValue(nookId, out var nook))
                return false;
            nook.Title = title;
            return true;
        }
    }

    public byte[] Read(string nookId, long offset, int maxBytes)
    {
        if (!TryGet(nookId, out var nook))
            return System.Array.Empty<byte>();
        long tail = nook.Ring.Tail;
        long head = nook.Ring.Head;
        long from = System.Math.Max(offset, tail);
        long avail = head - from;
        if (avail <= 0)
            return System.Array.Empty<byte>();
        int len = (int)System.Math.Min(maxBytes, avail);
        var buf = new byte[len];
        var res = nook.Ring.ReadInto(from, buf);
        return res.BytesCopied == len ? buf : buf[..res.BytesCopied];
    }

    private static void Terminate(NookSession nook, ILogger logger)
    {
        try { nook.Session.Kill(); } catch (System.Exception ex) { logger.NookTerminateStepFailed(nook.NookId, "kill", ex.Message); }
        try { nook.Reader.Dispose(); } catch (System.Exception ex) { logger.NookTerminateStepFailed(nook.NookId, "reader-dispose", ex.Message); }
        try { nook.Session.Dispose(); } catch (System.Exception ex) { logger.NookTerminateStepFailed(nook.NookId, "session-dispose", ex.Message); }
    }

    public void Dispose()
    {
        NookSession[] all;
        lock (_sync)
        {
            all = new NookSession[_nooks.Count];
            _nooks.Values.CopyTo(all, 0);
            _nooks.Clear();
        }
        foreach (NookSession nook in all)
            Terminate(nook, _logger);
    }
}
