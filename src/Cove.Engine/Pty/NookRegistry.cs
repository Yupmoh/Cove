using System;
using System.Collections.Generic;
using Cove.Persistence;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Pty;

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
    public NookInfo Spawn(SpawnParams p, string? defaultCwd = null)
    {
        string nookId = "nook-" + System.Guid.NewGuid().ToString("N");
        string? inherited = (!string.IsNullOrEmpty(p.InheritCwdFrom) && TryGet(p.InheritCwdFrom!, out var src)) ? src.Cwd : null;
        string? fallback = !string.IsNullOrEmpty(defaultCwd) ? defaultCwd : _projectDir;
        string cwd = ResolveWorkingDirectory(inherited, p.Cwd, fallback);
        var info = SpawnCore(nookId, p.Command, p.Args ?? System.Array.Empty<string>(), cwd, p.Cols, p.Rows, p.Env);
        Tag(nookId, p.Adapter, p.AgentName);
        return info;
    }

    public NookInfo RespawnAs(string nookId, string command, string[] args, string cwd, int cols, int rows, byte[]? priorScrollback = null, string? adapter = null, string? agentName = null)
    {
        var info = SpawnCore(nookId, command, args, cwd, cols, rows, null, priorScrollback);
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
        var ring = new PtyRingBuffer();
        if (priorScrollback is { Length: > 0 })
            ring.Append(priorScrollback);
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(session, ring, signal, _logger);
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
                arr[i++] = new NookDescriptor(p.NookId, p.Command, p.Args, string.IsNullOrEmpty(p.Cwd) ? p.SpawnCwd : p.Cwd!, p.Title, p.Adapter, p.AgentName);
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

    public bool Write(string nookId, ReadOnlySpan<byte> data)
    {
        if (!TryGet(nookId, out NookSession nook))
            return false;
        nook.Session.Write(data);
        return true;
    }

    public bool Resize(string nookId, int cols, int rows)
    {
        if (!TryGet(nookId, out NookSession nook))
            return false;
        nook.Session.Resize(cols, rows);
        nook.Cols = cols;
        nook.Rows = rows;
        return true;
    }

    public bool Kill(string nookId)
    {
        NookSession? nook;
        lock (_sync)
        {
            if (!_nooks.TryGetValue(nookId, out nook))
                return false;
            _nooks.Remove(nookId);
        }
        Terminate(nook);
        return true;
    }

    public bool Stop(string nookId)
    {
        lock (_sync)
        {
            if (!_nooks.TryGetValue(nookId, out var nook))
                return false;
            try { return nook.Session.Signal(Cove.Platform.Pty.PtyConstants.SigTerm); }
            catch { return false; }
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
        const int cap = 262144;
        long head = nook.Ring.Head;
        long tail = nook.Ring.Tail;
        long avail = head - tail;
        if (avail <= 0)
            return System.Array.Empty<byte>();
        int len = (int)System.Math.Min(cap, avail);
        long from = head - len;
        var buf = new byte[len];
        var res = nook.Ring.ReadInto(from, buf);
        return res.BytesCopied == len ? buf : buf[..res.BytesCopied];
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

    private static void Terminate(NookSession nook)
    {
        try { nook.Session.Kill(); } catch { }
        try { nook.Reader.Dispose(); } catch { }
        try { nook.Session.Dispose(); } catch { }
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
            Terminate(nook);
    }
}
