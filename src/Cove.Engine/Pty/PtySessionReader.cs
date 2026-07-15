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
    private readonly string _nookId;
    private readonly Osc7Parser _osc7 = new();
    private readonly TerminalModeTracker _terminalModes = new();
    private Thread? _thread;
    private volatile bool _completed;
    private int _exitCode = -1;
    private long _totalBytes;
    private bool _firstOutputSeen;

    public Action<string>? OnCwd { get; set; }

    public PtySessionReader(IPtySession session, PtyRingBuffer ring, PtyRingSignal signal, ILogger logger, string? nookId = null)
    {
        _session = session;
        _ring = ring;
        _signal = signal;
        _logger = logger;
        _nookId = string.IsNullOrEmpty(nookId) ? session.SessionId.ToString(System.Globalization.CultureInfo.InvariantCulture) : nookId!;
    }

    public bool HasCompleted => _completed;
    public int ExitCode => _exitCode;
    public string TerminalModePreamble => _terminalModes.BuildPreamble();
    public string TerminalCheckpointModeSupplement => _terminalModes.BuildCheckpointSupplement();

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
        _logger.ReaderLoopStarted(_nookId, _session.SessionId);
        var buffer = new byte[PtyConstants.ReadBufferBytes];
        try
        {
            while (true)
            {
                int n = _session.Read(buffer);
                if (n == 0)
                {
                    _logger.ReaderEof(_nookId, _totalBytes);
                    break;
                }
                _terminalModes.Feed(buffer.AsSpan(0, n));
                _ring.Append(buffer.AsSpan(0, n));
                _totalBytes += n;
                if (!_firstOutputSeen)
                {
                    _firstOutputSeen = true;
                    _logger.ReaderFirstOutput(n, _nookId);
                }
                else
                {
                    _logger.ReaderRead(_nookId, n);
                }
                var cwd = _osc7.Feed(buffer.AsSpan(0, n));
                if (cwd is not null)
                    OnCwd?.Invoke(cwd);
                _signal.Set();
            }
        }
        catch (PtyIoException ex)
        {
            _logger.ReaderError(_nookId, _session.SessionId, ex.Errno, ex.Message);
        }
        finally
        {
            _exitCode = _session.WaitForExit();
            _completed = true;
            _logger.ReaderExit(_nookId, _exitCode);
            _signal.Set();
        }
    }

    public void Dispose()
    {
        _session.Kill();
        _thread?.Join(TimeSpan.FromSeconds(5));
    }
}
