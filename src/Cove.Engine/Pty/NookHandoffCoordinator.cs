using Cove.Platform.Pty;
using Cove.Platform.Pty.Unix;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Pty;

internal sealed class NookHandoffCoordinator
{
    private readonly IPtyHost _host;
    private readonly NookSessionOwner _sessions;
    private readonly NookTerminalState _terminalState;
    private readonly ILogger _logger;

    public NookHandoffCoordinator(
        IPtyHost host,
        NookSessionOwner sessions,
        NookTerminalState terminalState,
        ILogger logger)
    {
        _host = host;
        _sessions = sessions;
        _terminalState = terminalState;
        _logger = logger;
    }

    public IReadOnlyList<HandoffExportItem> Export()
    {
        var sessions = _sessions.Snapshot();
        var items = new List<HandoffExportItem>(sessions.Length);
        foreach (var session in sessions)
        {
            if (session.Reader.HasCompleted)
            {
                _logger.HandoffSkipExited(session.NookId);
                continue;
            }
            if (!_host.TryExportSession(session.Session, out var fd, out var pid))
            {
                _logger.HandoffExportUnsupported(session.NookId);
                continue;
            }

            int transferFd;
            try
            {
                transferFd = UnixFd.Duplicate(fd);
            }
            catch (PtyIoException ex)
            {
                _logger.HandoffAdoptRejected(session.NookId, $"export dup failed: {ex.Message}");
                continue;
            }

            if (!_sessions.TryTake(session))
            {
                UnixFdChannel.CloseFd(transferFd);
                continue;
            }

            try
            {
                session.DetachForHandoff();
                var state = _terminalState.CaptureForHandoff(session);
                var record = new HandoffNookRecord(
                    session.NookId,
                    pid,
                    session.Command,
                    session.Args,
                    session.SpawnCwd,
                    session.Cwd,
                    session.Cols,
                    session.Rows,
                    session.Title,
                    session.Adapter,
                    session.AgentName,
                    state.RingHead,
                    state.RingTail.Length,
                    null,
                    null,
                    state.Checkpoint,
                    session.Token);
                _logger.HandoffExported(session.NookId, pid, state.RingTail.Length);
                items.Add(new HandoffExportItem(record, transferFd, state.RingTail));
            }
            catch
            {
                UnixFdChannel.CloseFd(transferFd);
                throw;
            }
        }
        return items;
    }

    public NookInfo? Adopt(HandoffNookRecord record, int masterFd, byte[] ringTail)
    {
        if (string.IsNullOrEmpty(record.NookToken))
        {
            _logger.HandoffAdoptRejected(record.NookId, "nook credential missing");
            return null;
        }
        if (record.Pid <= 0 || masterFd < 0)
        {
            _logger.HandoffAdoptRejected(
                record.NookId,
                $"invalid transfer fd={masterFd} pid={record.Pid}");
            return null;
        }
        if (!OperatingSystem.IsWindows()
            && ProcessExitWatch.WaitForExitAsync(record.Pid).IsCompleted)
        {
            _logger.HandoffAdoptRejected(
                record.NookId,
                $"pid {record.Pid} already exited");
            return null;
        }

        IPtySession ptySession;
        try
        {
            ptySession = _host.AdoptSession(masterFd, record.Pid);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or ArgumentOutOfRangeException)
        {
            _logger.HandoffAdoptRejected(record.NookId, ex.Message);
            return null;
        }

        PtyRingBuffer ring;
        try
        {
            ring = _terminalState.RestoreRing(record.RingHead, ringTail);
        }
        catch (ArgumentException ex)
        {
            _logger.HandoffAdoptRejected(record.NookId, ex.Message);
            ptySession.Dispose();
            return null;
        }

        var signal = new PtyRingSignal();
        var reader = new PtySessionReader(ptySession, ring, signal, _logger, record.NookId);
        var session = new NookSession(
            record.NookId,
            record.Command,
            record.Args,
            record.SpawnCwd,
            record.Cols,
            record.Rows,
            ptySession,
            ring,
            signal,
            reader,
            record.NookToken,
            _logger)
        {
            Cwd = record.Cwd,
            PendingRepaint = true,
            Title = record.Title,
            Adapter = record.Adapter,
            AgentName = record.AgentName,
        };

        try
        {
            _terminalState.RestoreCheckpoint(session, record.Checkpoint);
        }
        catch (FormatException ex)
        {
            _logger.HandoffAdoptRejected(record.NookId, ex.Message);
            session.Dispose();
            return null;
        }

        reader.OnCwd = cwd => session.Cwd = cwd;
        try
        {
            ptySession.Resize(record.Cols, record.Rows);
        }
        catch (PtyIoException ex)
        {
            _logger.HandoffAdoptRejected(
                record.NookId,
                "resize replay failed: " + ex.Message);
        }

        _sessions.Replace(session);
        reader.Start();
        _logger.HandoffAdopted(record.NookId, record.Pid, record.RingHead);
        return session.ToInfo();
    }
}
