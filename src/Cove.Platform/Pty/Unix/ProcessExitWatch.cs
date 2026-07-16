using System.Collections.Concurrent;

namespace Cove.Platform.Pty.Unix;

public static class ProcessExitWatch
{
    private static readonly Lazy<Watcher> Shared = new(() => new Watcher(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static Task WaitForExitAsync(int pid, CancellationToken cancellationToken = default)
        => Shared.Value.Register(pid, cancellationToken);

    private sealed class Watcher
    {
        private readonly int _watchFd;
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _pending = new();

        internal Watcher()
        {
            var fd = CovePtyNative.ExitWatchNew();
            if (fd < 0)
                throw new PtyIoException($"exit watcher creation failed (errno {-fd}).", -fd);
            _watchFd = fd;
            var loop = new Thread(RunLoop) { IsBackground = true, Name = "cove-exit-watch" };
            loop.Start();
        }

        internal Task Register(int pid, CancellationToken cancellationToken)
        {
            var tcs = _pending.GetOrAdd(pid, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            var rc = CovePtyNative.ExitWatchAdd(_watchFd, pid);
            if (rc == 1)
            {
                _pending.TryRemove(pid, out _);
                tcs.TrySetResult();
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
                var pid = CovePtyNative.ExitWatchNext(_watchFd);
                if (pid > 0)
                {
                    if (_pending.TryRemove(pid, out var tcs))
                        tcs.TrySetResult();
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
