using System;
using System.Threading;
using Cove.Platform.Pty;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Pty;

public sealed class PtySessionReader : IDisposable
{
    private readonly IPtySession _session;
    private readonly PtyRingBuffer _ring;
    private readonly PtyRingSignal _signal;
    private readonly ILogger _logger;
    private readonly Osc7Parser _osc7 = new();
    private Thread? _thread;
    private volatile bool _completed;
    private int _exitCode = -1;

    public Action<string>? OnCwd { get; set; }

    public PtySessionReader(IPtySession session, PtyRingBuffer ring, PtyRingSignal signal, ILogger logger)
    {
        _session = session;
        _ring = ring;
        _signal = signal;
        _logger = logger;
    }

    public bool HasCompleted => _completed;
    public int ExitCode => _exitCode;

    public void Start()
    {
        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Name = $"cove-pty-drain-{_session.SessionId}",
        };
        _thread.Start();
    }

    private void RunLoop()
    {
        var buffer = new byte[PtyConstants.ReadBufferBytes];
        try
        {
            while (true)
            {
                int n = _session.Read(buffer);
                if (n == 0)
                    break;
                _ring.Append(buffer.AsSpan(0, n));
                var cwd = _osc7.Feed(buffer.AsSpan(0, n));
                if (cwd is not null)
                    OnCwd?.Invoke(cwd);
                _signal.Set();
            }
        }
        catch (PtyIoException ex)
        {
            _logger.LogError(ex, "pty drain read error (session {Id}, errno {Errno}).", _session.SessionId, ex.Errno);
        }
        finally
        {
            _exitCode = _session.WaitForExit();
            _completed = true;
            _signal.Set();
        }
    }

    public void Dispose()
    {
        _thread?.Join(TimeSpan.FromSeconds(5));
    }
}
