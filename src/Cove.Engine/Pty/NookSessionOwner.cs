using Cove.Persistence;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Pty;

internal sealed record TerminalCheckpoint(byte[] Data, long Offset, int Cols, int Rows, int ScrollbackLines, string ModeSupplement);

internal sealed class NookSession : IDisposable
{
    private readonly ILogger _logger;
    private int _ownershipEnded;

    public NookSession(
        string nookId,
        string command,
        string[] args,
        string spawnCwd,
        int cols,
        int rows,
        IPtySession session,
        PtyRingBuffer ring,
        PtyRingSignal signal,
        PtySessionReader reader,
        string token,
        ILogger logger)
    {
        NookId = nookId;
        Command = command;
        Args = args;
        SpawnCwd = spawnCwd;
        Cols = cols;
        Rows = rows;
        Session = session;
        Ring = ring;
        Signal = signal;
        Reader = reader;
        Token = token;
        _logger = logger;
    }

    public string NookId { get; }
    public string Command { get; }
    public string[] Args { get; }
    public string SpawnCwd { get; }
    public int Cols { get; set; }
    public int Rows { get; set; }
    public string? Cwd { get; set; }
    public string? Title { get; set; }
    public string? Adapter { get; set; }
    public string? AgentName { get; set; }
    public IPtySession Session { get; }
    public PtyRingBuffer Ring { get; }
    public PtyRingSignal Signal { get; }
    public PtySessionReader Reader { get; }
    public TerminalCheckpoint? Checkpoint { get; set; }
    public bool PendingRepaint { get; set; }
    public string Token { get; }

    public NookInfo ToInfo() => new(NookId, Command, Cols, Rows, !Reader.HasCompleted, Cwd, Title);

    public void DetachForHandoff()
    {
        if (Interlocked.Exchange(ref _ownershipEnded, 1) != 0)
            throw new ObjectDisposedException(nameof(NookSession));
        Reader.DetachForHandoff();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _ownershipEnded, 1) != 0)
            return;
        try
        {
            Reader.Dispose();
        }
        catch (Exception ex)
        {
            _logger.NookTerminateStepFailed(NookId, "reader-dispose", ex.Message);
        }
        try
        {
            Session.Dispose();
        }
        catch (Exception ex)
        {
            _logger.NookTerminateStepFailed(NookId, "session-dispose", ex.Message);
        }
    }
}

internal sealed class NookSessionOwner : IDisposable
{
    private readonly object _sync = new();
    private readonly Dictionary<string, NookSession> _sessions = new();
    private bool _disposed;

    public bool Contains(string nookId)
    {
        lock (_sync)
            return _sessions.ContainsKey(nookId);
    }

    public bool TryGet(string nookId, out NookSession session)
    {
        lock (_sync)
            return _sessions.TryGetValue(nookId, out session!);
    }

    public string[] NookIds()
    {
        lock (_sync)
            return [.. _sessions.Keys];
    }

    public NookSession[] Snapshot()
    {
        lock (_sync)
            return [.. _sessions.Values];
    }

    public void Replace(NookSession session)
    {
        NookSession? prior;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _sessions.TryGetValue(session.NookId, out prior);
            _sessions[session.NookId] = session;
        }
        prior?.Dispose();
    }

    public bool TryTake(NookSession session)
    {
        lock (_sync)
        {
            if (!_sessions.TryGetValue(session.NookId, out var current) || !ReferenceEquals(current, session))
                return false;
            _sessions.Remove(session.NookId);
            return true;
        }
    }

    public bool Terminate(string nookId)
    {
        NookSession? session;
        lock (_sync)
        {
            if (!_sessions.Remove(nookId, out session))
                return false;
        }
        session.Dispose();
        return true;
    }

    public NookDescriptor[] Descriptors()
    {
        lock (_sync)
        {
            var result = new NookDescriptor[_sessions.Count];
            var index = 0;
            foreach (var session in _sessions.Values)
            {
                result[index++] = new NookDescriptor(
                    session.NookId,
                    session.Command,
                    session.Args,
                    string.IsNullOrEmpty(session.Cwd) ? session.SpawnCwd : session.Cwd,
                    session.Title,
                    session.Adapter,
                    session.AgentName,
                    null,
                    false,
                    session.Cols,
                    session.Rows);
            }
            return result;
        }
    }

    public NookInfo[] List()
    {
        lock (_sync)
        {
            var result = new NookInfo[_sessions.Count];
            var index = 0;
            foreach (var session in _sessions.Values)
                result[index++] = session.ToInfo();
            return result;
        }
    }

    public List<(string NookId, string Adapter)> ListAdapterNooks()
    {
        lock (_sync)
        {
            var result = new List<(string, string)>();
            foreach (var session in _sessions.Values)
            {
                if (!string.IsNullOrEmpty(session.Adapter))
                    result.Add((session.NookId, session.Adapter));
            }
            return result;
        }
    }

    public void Tag(string nookId, string? adapter, string? agentName)
    {
        if (string.IsNullOrEmpty(adapter))
            return;
        lock (_sync)
        {
            if (_sessions.TryGetValue(nookId, out var session))
            {
                session.Adapter = adapter;
                session.AgentName = agentName;
            }
        }
    }

    public bool Rename(string nookId, string title)
    {
        lock (_sync)
        {
            if (!_sessions.TryGetValue(nookId, out var session))
                return false;
            session.Title = title;
            return true;
        }
    }

    public bool ConsumePendingRepaint(string nookId)
    {
        lock (_sync)
        {
            if (!_sessions.TryGetValue(nookId, out var session) || !session.PendingRepaint)
                return false;
            session.PendingRepaint = false;
            return true;
        }
    }

    public void Dispose()
    {
        NookSession[] sessions;
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            sessions = [.. _sessions.Values];
            _sessions.Clear();
        }
        foreach (var session in sessions)
            session.Dispose();
    }
}
