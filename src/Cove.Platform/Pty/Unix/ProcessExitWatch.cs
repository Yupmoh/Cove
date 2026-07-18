using System.Collections.Concurrent;

namespace Cove.Platform.Pty.Unix;

public static class ProcessExitWatch
{
    private static readonly Lazy<Watcher> Shared = new(() => new Watcher(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static Task<int> WaitForExitAsync(int pid, CancellationToken cancellationToken = default)
        => Shared.Value.Register(pid, cancellationToken);

    internal static bool TryObserveExit(Task<int> observation, TimeSpan timeout, out int exitCode)
    {
        if (!observation.Wait(timeout))
        {
            exitCode = -1;
            return false;
        }
        exitCode = DecodeWaitStatus(observation.Result);
        return true;
    }

    public static int DecodeWaitStatus(int status)
    {
        if (status < 0)
            return -1;
        if ((status & 0x7f) == 0)
            return (status >> 8) & 0xff;
        if ((status & 0x7f) != 0x7f)
            return 128 + (status & 0x7f);
        return -1;
    }

    private sealed class Watcher
    {
        private readonly int _watchFd;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<int>> _pending = new();

        internal Watcher()
        {
            var fd = CovePtyNative.ExitWatchNew();
            if (fd < 0)
                throw new PtyIoException($"exit watcher creation failed (errno {-fd}).", -fd);
            _watchFd = fd;
            var loop = new Thread(RunLoop) { IsBackground = true, Name = "cove-exit-watch" };
            loop.Start();
        }

        internal Task<int> Register(int pid, CancellationToken cancellationToken)
        {
            var tcs = _pending.GetOrAdd(pid, _ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously));
            var rc = CovePtyNative.ExitWatchAdd(_watchFd, pid);
            if (rc == 1)
            {
                _pending.TryRemove(pid, out _);
                tcs.TrySetResult(-1);
            }
            else if (rc < 0)
            {
                _pending.TryRemove(pid, out _);
                tcs.TrySetException(new PtyIoException($"exit watch registration failed (pid {pid}, errno {-rc}).", -rc));
            }
            return cancellationToken.CanBeCanceled ? tcs.Task.WaitAsync(cancellationToken) : tcs.Task;
        }

        private void RunLoop()
        {
            for (; ; )
            {
                var pid = CovePtyNative.ExitWatchNext(_watchFd, out var status);
                if (pid > 0)
                {
                    if (_pending.TryRemove(pid, out var tcs))
                        tcs.TrySetResult(status);
                    continue;
                }
                foreach (var pending in _pending)
                {
                    if (_pending.TryRemove(pending.Key, out var tcs))
                        tcs.TrySetException(new PtyIoException($"exit watcher loop failed (errno {-pid}).", -pid));
                }
                return;
            }
        }
    }
}
