using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Pty;

internal sealed record NookStreamState(
    long SessionId,
    PtyRingBuffer Ring,
    PtyRingSignal Signal,
    int Cols,
    int Rows,
    Func<string> ModePreamble,
    Func<bool> HasCompleted,
    Func<int> ExitCode);

internal sealed record TerminalHandoffState(
    long RingHead,
    byte[] RingTail,
    HandoffCheckpointDto? Checkpoint);

internal sealed class NookTerminalState
{
    private readonly NookSessionOwner _sessions;
    private readonly ILogger _logger;

    public NookTerminalState(NookSessionOwner sessions, ILogger logger)
    {
        _sessions = sessions;
        _logger = logger;
    }

    public bool ConsumePendingRepaint(string nookId) => _sessions.ConsumePendingRepaint(nookId);

    public (long Head, byte[] Delta)? TryGetScreenSample(string nookId, long sinceOffset, int maxBytes)
    {
        if (!_sessions.TryGet(nookId, out var session) || session.Reader.HasCompleted)
            return null;
        var head = session.Ring.Head;
        var from = Math.Max(session.Ring.Tail, Math.Max(sinceOffset, head - maxBytes));
        if (from >= head)
            return (head, []);
        if (!session.Ring.TrySnapshotFrom(from, out var delta))
            return null;
        return (head, delta);
    }

    public SearchMatch[] Search(string nookId, string query, bool caseSensitive)
    {
        if (!_sessions.TryGet(nookId, out var session))
            return [];
        var tail = session.Ring.Tail;
        var head = session.Ring.Head;
        var length = (int)Math.Min(session.Ring.Capacity, head - tail);
        if (length <= 0)
            return [];
        var buffer = new byte[length];
        var result = session.Ring.ReadInto(tail, buffer);
        return RingSearch.Find(buffer.AsSpan(0, result.BytesCopied), query, caseSensitive);
    }

    public byte[] SnapshotRing(string nookId) =>
        _sessions.TryGet(nookId, out var session) ? session.Ring.Snapshot() : [];

    public bool StoreTerminalCheckpoint(string nookId, byte[] checkpoint, long offset, int cols, int rows, int scrollbackLines)
    {
        if (!_sessions.TryGet(nookId, out var session))
        {
            _logger.TerminalCheckpointUnknownNook(nookId);
            return false;
        }
        if (checkpoint.Length == 0
            || offset < 0
            || cols < 1
            || rows < 1
            || scrollbackLines < 0
            || !session.Ring.ContainsOffset(offset))
        {
            _logger.TerminalCheckpointRejected(
                nookId,
                offset,
                session.Ring.Tail,
                session.Ring.Head,
                cols,
                rows,
                checkpoint.Length);
            return false;
        }
        lock (session)
        {
            session.Checkpoint = new TerminalCheckpoint(
                checkpoint,
                offset,
                cols,
                rows,
                scrollbackLines,
                session.Reader.TerminalCheckpointModeSupplement);
        }
        return true;
    }

    public bool StoreTerminalCheckpointBase64(string nookId, string checkpointBase64, long offset, int cols, int rows, int scrollbackLines)
    {
        try
        {
            return StoreTerminalCheckpoint(
                nookId,
                Convert.FromBase64String(checkpointBase64),
                offset,
                cols,
                rows,
                scrollbackLines);
        }
        catch (FormatException ex)
        {
            _logger.TerminalCheckpointDecodeFailed(nookId, ex.Message);
            return false;
        }
    }

    public TerminalCheckpoint? GetTerminalCheckpoint(string nookId)
    {
        if (!_sessions.TryGet(nookId, out var session))
        {
            _logger.TerminalCheckpointUnknownNook(nookId);
            return null;
        }
        TerminalCheckpoint? checkpoint;
        lock (session)
            checkpoint = session.Checkpoint;
        if (checkpoint is not null && !session.Ring.ContainsOffset(checkpoint.Offset))
        {
            _logger.TerminalCheckpointExpired(
                nookId,
                checkpoint.Offset,
                session.Ring.Tail,
                session.Ring.Head);
            return null;
        }
        return checkpoint;
    }

    public TerminalRestoreState? CaptureTerminalRestoreState(string nookId)
    {
        var checkpoint = GetTerminalCheckpoint(nookId);
        if (checkpoint is null || !_sessions.TryGet(nookId, out var session))
            return null;
        if (!session.Ring.TrySnapshotFrom(checkpoint.Offset, out var tail))
        {
            _logger.TerminalCheckpointExpired(
                nookId,
                checkpoint.Offset,
                session.Ring.Tail,
                session.Ring.Head);
            return null;
        }
        return new TerminalRestoreState(
            checkpoint.Data,
            tail,
            checkpoint.Offset,
            checkpoint.Cols,
            checkpoint.Rows,
            checkpoint.ScrollbackLines,
            checkpoint.ModeSupplement);
    }

    public void RestoreCheckpoint(NookSession session, TerminalRestoreState restoreState)
    {
        lock (session)
        {
            session.Checkpoint = new TerminalCheckpoint(
                restoreState.Checkpoint,
                0,
                restoreState.Cols,
                restoreState.Rows,
                restoreState.ScrollbackLines,
                restoreState.ModeSupplement);
        }
    }

    public void RestoreCheckpoint(NookSession session, HandoffCheckpointDto? checkpoint)
    {
        if (checkpoint is null)
            return;
        lock (session)
        {
            session.Checkpoint = new TerminalCheckpoint(
                Convert.FromBase64String(checkpoint.DataBase64),
                checkpoint.Offset,
                checkpoint.Cols,
                checkpoint.Rows,
                checkpoint.ScrollbackLines,
                checkpoint.ModeSupplement);
        }
    }

    public TerminalHandoffState CaptureForHandoff(NookSession session)
    {
        TerminalCheckpoint? checkpoint;
        lock (session)
            checkpoint = session.Checkpoint;
        var dto = checkpoint is null
            ? null
            : new HandoffCheckpointDto(
                Convert.ToBase64String(checkpoint.Data),
                checkpoint.Offset,
                checkpoint.Cols,
                checkpoint.Rows,
                checkpoint.ScrollbackLines,
                checkpoint.ModeSupplement);
        return new TerminalHandoffState(session.Ring.Head, session.Ring.Snapshot(), dto);
    }

    public PtyRingBuffer RestoreRing(long ringHead, byte[] ringTail)
    {
        var ring = new PtyRingBuffer();
        ring.RestoreAt(ringHead, ringTail);
        return ring;
    }

    public byte[] Read(string nookId, long offset, int maxBytes)
    {
        if (!_sessions.TryGet(nookId, out var session))
            return [];
        var tail = session.Ring.Tail;
        var head = session.Ring.Head;
        var from = Math.Max(offset, tail);
        var available = head - from;
        if (available <= 0)
            return [];
        var length = (int)Math.Min(maxBytes, available);
        var buffer = new byte[length];
        var result = session.Ring.ReadInto(from, buffer);
        return result.BytesCopied == length ? buffer : buffer[..result.BytesCopied];
    }

    public long TryGetHead(string nookId) =>
        _sessions.TryGet(nookId, out var session) ? session.Ring.Head : 0;

    public bool TryGetStreamState(string nookId, out NookStreamState state)
    {
        if (!_sessions.TryGet(nookId, out var session))
        {
            state = null!;
            return false;
        }
        state = new NookStreamState(
            session.Session.SessionId,
            session.Ring,
            session.Signal,
            session.Cols,
            session.Rows,
            () => session.Reader.TerminalModePreamble,
            () => session.Reader.HasCompleted,
            () => session.Reader.ExitCode);
        return true;
    }
}
