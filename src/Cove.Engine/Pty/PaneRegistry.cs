using System;
using System.Collections.Generic;
using Cove.Persistence;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Pty;

internal sealed class PaneSession
{
    public required string PaneId { get; init; }
    public required string Command { get; init; }
    public required string[] Args { get; init; }
    public required string SpawnCwd { get; init; }
    public int Cols { get; set; }
    public int Rows { get; set; }
    public string? Cwd { get; set; }
    public required IPtySession Session { get; init; }
    public required PtyRingBuffer Ring { get; init; }
    public required PtyRingSignal Signal { get; init; }
    public required PtySessionReader Reader { get; init; }

    public PaneInfo ToInfo() => new(PaneId, Command, Cols, Rows, !Reader.HasCompleted, Cwd);
}

public sealed class PaneRegistry : IDisposable
{
    private readonly IPtyHost _host;
    private readonly ILogger _logger;
    private readonly SpawnEnvironment? _spawnEnv;
    private readonly string? _shellDir;
    private readonly object _sync = new();
    private readonly Dictionary<string, PaneSession> _panes = new();

    public PaneRegistry(IPtyHost host, ILogger logger, SpawnEnvironment? spawnEnv = null, string? shellDir = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(logger);
        _host = host;
        _logger = logger;
        _spawnEnv = spawnEnv;
        _shellDir = shellDir;
    }

    public PaneInfo Spawn(SpawnParams p)
    {
        string paneId = "pane-" + System.Guid.NewGuid().ToString("N");
        string? inherited = (!string.IsNullOrEmpty(p.InheritCwdFrom) && TryGet(p.InheritCwdFrom!, out var src)) ? src.Cwd : null;
        string cwd = ResolveWorkingDirectory(inherited, p.Cwd);
        return SpawnCore(paneId, p.Command, p.Args ?? System.Array.Empty<string>(), cwd, p.Cols, p.Rows, p.Env);
    }

    public PaneInfo RespawnAs(string paneId, string command, string[] args, string cwd, int cols, int rows, byte[]? priorScrollback = null)
        => SpawnCore(paneId, command, args, cwd, cols, rows, null, priorScrollback);

    private PaneInfo SpawnCore(string paneId, string command, string[] args, string cwd, int cols, int rows, System.Collections.Generic.IReadOnlyDictionary<string, string>? callerEnv, byte[]? priorScrollback = null)
    {
        var envDict = _spawnEnv is { } se ? se.Build(paneId, callerEnv) : callerEnv;
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
        var ring = new PtyRingBuffer();
        if (priorScrollback is { Length: > 0 })
            ring.Append(priorScrollback);
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(session, ring, signal, _logger);
        var pane = new PaneSession
        {
            PaneId = paneId,
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
        reader.OnCwd = c => pane.Cwd = c;
        reader.Start();
        lock (_sync)
            _panes[paneId] = pane;
        return pane.ToInfo();
    }

    public PaneDescriptor[] Descriptors()
    {
        lock (_sync)
        {
            var arr = new PaneDescriptor[_panes.Count];
            int i = 0;
            foreach (var p in _panes.Values)
                arr[i++] = new PaneDescriptor(p.PaneId, p.Command, p.Args, string.IsNullOrEmpty(p.Cwd) ? p.SpawnCwd : p.Cwd!);
            return arr;
        }
    }

    public static string ResolveWorkingDirectory(string? inheritedCwd, string? explicitCwd) =>
        !string.IsNullOrEmpty(inheritedCwd) ? inheritedCwd! : (!string.IsNullOrEmpty(explicitCwd) ? explicitCwd! : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    internal bool TryGet(string paneId, out PaneSession pane)
    {
        lock (_sync)
            return _panes.TryGetValue(paneId, out pane!);
    }

    public PaneInfo[] List()
    {
        lock (_sync)
        {
            var arr = new PaneInfo[_panes.Count];
            int i = 0;
            foreach (PaneSession p in _panes.Values)
                arr[i++] = p.ToInfo();
            return arr;
        }
    }

    public bool Write(string paneId, ReadOnlySpan<byte> data)
    {
        if (!TryGet(paneId, out PaneSession pane))
            return false;
        pane.Session.Write(data);
        return true;
    }

    public bool Resize(string paneId, int cols, int rows)
    {
        if (!TryGet(paneId, out PaneSession pane))
            return false;
        pane.Session.Resize(cols, rows);
        pane.Cols = cols;
        pane.Rows = rows;
        return true;
    }

    public bool Kill(string paneId)
    {
        PaneSession? pane;
        lock (_sync)
        {
            if (!_panes.TryGetValue(paneId, out pane))
                return false;
            _panes.Remove(paneId);
        }
        Terminate(pane);
        return true;
    }

    public bool Stop(string paneId)
    {
        lock (_sync)
        {
            if (!_panes.TryGetValue(paneId, out var pane))
                return false;
            try { return pane.Session.Signal(Cove.Platform.Pty.PtyConstants.SigTerm); }
            catch { return false; }
        }
    }

    public SearchMatch[] Search(string paneId, string query, bool caseSensitive)
    {
        if (!TryGet(paneId, out var pane))
            return Array.Empty<SearchMatch>();
        long tail = pane.Ring.Tail;
        long head = pane.Ring.Head;
        int len = (int)Math.Min(pane.Ring.Capacity, head - tail);
        if (len <= 0)
            return Array.Empty<SearchMatch>();
        var buf = new byte[len];
        var res = pane.Ring.ReadInto(tail, buf);
        return RingSearch.Find(buf.AsSpan(0, res.BytesCopied), query, caseSensitive);
    }

    public byte[] SnapshotRing(string paneId)
    {
        if (!TryGet(paneId, out var pane))
            return System.Array.Empty<byte>();
        const int cap = 262144;
        long head = pane.Ring.Head;
        long tail = pane.Ring.Tail;
        long avail = head - tail;
        if (avail <= 0)
            return System.Array.Empty<byte>();
        int len = (int)System.Math.Min(cap, avail);
        long from = head - len;
        var buf = new byte[len];
        var res = pane.Ring.ReadInto(from, buf);
        return res.BytesCopied == len ? buf : buf[..res.BytesCopied];
    }

    private static void Terminate(PaneSession pane)
    {
        try { pane.Session.Kill(); } catch { }
        try { pane.Reader.Dispose(); } catch { }
        try { pane.Session.Dispose(); } catch { }
    }

    public void Dispose()
    {
        PaneSession[] all;
        lock (_sync)
        {
            all = new PaneSession[_panes.Count];
            _panes.Values.CopyTo(all, 0);
            _panes.Clear();
        }
        foreach (PaneSession pane in all)
            Terminate(pane);
    }
}
