using System;
using System.Collections.Generic;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Pty;

internal sealed class PaneSession
{
    public required string PaneId { get; init; }
    public required string Command { get; init; }
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
        var envDict = _spawnEnv is { } se ? se.Build(paneId, p.Env) : p.Env;
        var args = p.Args ?? Array.Empty<string>();
        if (envDict is Dictionary<string, string> ed && _shellDir is { } sd)
            args = (string[])System.Linq.Enumerable.ToArray(ShellIntegration.Apply(p.Command, sd, args, ed));
        var request = new PtySpawnRequest
        {
            Command = p.Command,
            Args = args,
            WorkingDirectory = string.IsNullOrEmpty(p.Cwd) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : p.Cwd,
            Environment = envDict,
            Cols = p.Cols,
            Rows = p.Rows,
        };
        IPtySession session = _host.Spawn(request);
        var ring = new PtyRingBuffer();
        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(session, ring, signal, _logger);
        var pane = new PaneSession
        {
            PaneId = paneId,
            Command = p.Command,
            Cols = p.Cols,
            Rows = p.Rows,
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
