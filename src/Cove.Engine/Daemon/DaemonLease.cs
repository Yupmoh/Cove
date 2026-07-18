using System.Text;
using Cove.Platform;
using Cove.Platform.Ipc;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Daemon;

internal readonly record struct DaemonLeaseAttempt(
    DaemonLease? Lease,
    int ExitCode)
{
    public bool Acquired => Lease is not null;
}

internal readonly record struct DaemonControlPublishResult(
    bool Published,
    int ExitCode);

internal sealed class DaemonLease : IAsyncDisposable
{
    private readonly DaemonPaths _paths;
    private readonly IControlEndpoint _endpoint;
    private readonly SingleInstanceGuard _dataDirLock;
    private readonly SingleInstanceGuard _channelGuard;
    private readonly ILogger _logger;
    private IControlListener? _listener;
    private string? _controlToken;
    private int _ownsPublishedEndpoint;
    private int _listenerClosed;
    private int _disposed;

    private DaemonLease(
        DaemonPaths paths,
        IControlEndpoint endpoint,
        SingleInstanceGuard dataDirLock,
        SingleInstanceGuard channelGuard,
        ILogger logger)
    {
        _paths = paths;
        _endpoint = endpoint;
        _dataDirLock = dataDirLock;
        _channelGuard = channelGuard;
        _logger = logger;
    }

    public IControlListener Listener =>
        _listener
        ?? throw new InvalidOperationException("control endpoint is not published");

    public string ControlToken =>
        _controlToken
        ?? throw new InvalidOperationException("control token is not published");

    public static DaemonLeaseAttempt TryAcquireOwnership(
        DaemonPaths paths,
        IControlEndpoint endpoint,
        bool retry,
        ILogger logger)
    {
        var dataDirLock = TryAcquireWithRetry(paths.DaemonLockPath, retry);
        if (dataDirLock is null)
        {
            var ownerPid = PidFile.Read(paths.DaemonLockPath);
            DaemonLog.Write(
                paths,
                "daemon already owns data dir "
                    + paths.DataDir.Root
                    + (ownerPid is { } pid ? " pid=" + pid : ""));
            logger.DataDirectoryAlreadyOwned(
                ownerPid?.ToString() ?? "unknown");
            return new DaemonLeaseAttempt(null, 1);
        }
        dataDirLock.WritePid(Environment.ProcessId);
        CoveTree.Ensure(paths.DataDir);

        var channelGuard = TryAcquireWithRetry(paths.PidFilePath, retry);
        if (channelGuard is null)
        {
            DaemonLog.Write(
                paths,
                "daemon already running on channel " + paths.Channel);
            logger.DaemonChannelAlreadyOwned(paths.Channel);
            dataDirLock.Dispose();
            return new DaemonLeaseAttempt(null, 0);
        }

        return new DaemonLeaseAttempt(
            new DaemonLease(
                paths,
                endpoint,
                dataDirLock,
                channelGuard,
                logger),
            0);
    }

    public DaemonControlPublishResult TryPublishControlEndpoint()
    {
        if (!OperatingSystem.IsWindows() && File.Exists(_paths.SocketPath))
        {
            if (_endpoint.TryProbe(250))
            {
                DaemonLog.Write(
                    _paths,
                    "stale_reclaim_conflict on channel " + _paths.Channel);
                _logger.DaemonEndpointAlreadyOwned(_paths.Channel);
                return new DaemonControlPublishResult(false, 1);
            }
            try
            {
                File.Delete(_paths.SocketPath);
            }
            catch (Exception ex)
            {
                DaemonLog.Write(
                    _paths,
                    "stale unlink failed: " + ex.Message);
            }
        }

        _controlToken = Convert.ToHexString(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        try
        {
            var tokenTemp = _paths.ControlTokenPath
                + "."
                + Guid.NewGuid().ToString("N")
                + ".tmp";
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode =
                    UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }
            using (var stream = new FileStream(tokenTemp, options))
                stream.Write(Encoding.ASCII.GetBytes(_controlToken));
            File.Move(tokenTemp, _paths.ControlTokenPath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.ControlTokenWriteFailed(
                _paths.ControlTokenPath,
                ex.Message);
            return new DaemonControlPublishResult(false, 1);
        }

        try
        {
            _listener = _endpoint.Bind();
        }
        catch (Exception ex)
        {
            DaemonLog.Write(
                _paths,
                "bind failed (already running?): " + ex.Message);
            _logger.DaemonControlBindFailed(
                _paths.Channel,
                ex.Message);
            return new DaemonControlPublishResult(false, 0);
        }

        _channelGuard.WritePid(Environment.ProcessId);
        Volatile.Write(ref _ownsPublishedEndpoint, 1);
        return new DaemonControlPublishResult(true, 0);
    }

    private static SingleInstanceGuard? TryAcquireWithRetry(
        string path,
        bool retry)
    {
        var guard = SingleInstanceGuard.TryAcquire(path);
        if (guard is not null || !retry)
            return guard;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 10_000)
        {
            Thread.Sleep(100);
            guard = SingleInstanceGuard.TryAcquire(path);
            if (guard is not null)
                return guard;
        }
        return null;
    }

    public async ValueTask CloseListenerAsync()
    {
        if (Interlocked.Exchange(ref _listenerClosed, 1) != 0)
            return;
        var listener = Interlocked.Exchange(ref _listener, null);
        if (listener is null)
            return;
        try
        {
            await listener.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.DaemonListenerCleanupFailed(ex.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        try
        {
            await CloseListenerAsync().ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (Volatile.Read(ref _ownsPublishedEndpoint) != 0)
                {
                    if (!OperatingSystem.IsWindows())
                    {
                        try
                        {
                            File.Delete(_paths.SocketPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.DaemonSocketCleanupFailed(
                                _paths.SocketPath,
                                ex.Message);
                        }
                    }
                    PidFile.Delete(_paths.PidFilePath);
                }
            }
            finally
            {
                try
                {
                    _channelGuard.Dispose();
                }
                finally
                {
                    _dataDirLock.Dispose();
                }
            }
        }
    }
}
