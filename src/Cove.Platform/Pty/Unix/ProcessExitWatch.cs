using System.Runtime.ExceptionServices;

namespace Cove.Platform.Pty.Unix;

public static class ProcessExitWatch
{
    private static readonly Lazy<Watcher> Shared = new(() => new Watcher(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static Task<int> WaitForExitAsync(int pid, CancellationToken cancellationToken = default)
        => Shared.Value.Register(pid, cancellationToken);

    internal static bool IsRegistered(int pid)
        => Shared.Value.IsRegistered(pid);

    internal static bool TryObserveExit(Task<int> observation, TimeSpan timeout, out int exitCode)
    {
        try
        {
            if (!observation.Wait(timeout))
            {
                exitCode = -1;
                return false;
            }
        }
        catch (AggregateException)
        {
            observation.GetAwaiter().GetResult();
            throw;
        }
        exitCode = DecodeWaitStatus(observation.GetAwaiter().GetResult());
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
        private readonly Lock _gate = new();
        private readonly Dictionary<int, Registration> _byPid = [];
        private readonly Dictionary<long, Registration> _byToken = [];
        private readonly nint _watch;
        private long _nextToken;

        internal Watcher()
        {
            _watch = CovePtyNative.ExitWatchNew();
            if (_watch < 0)
                throw new PtyIoException($"exit watcher creation failed (errno {-(long)_watch}).", (int)-(long)_watch);
            var loop = new Thread(RunLoop) { IsBackground = true, Name = "cove-exit-watch" };
            loop.Start();
        }

        internal Task<int> Register(int pid, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_byPid.TryGetValue(pid, out var existing))
                {
                    existing.ObserverCount++;
                    return ObserveAsync(existing, cancellationToken);
                }

                var token = NextToken();
                var registration = new Registration(pid, token);
                var rc = CovePtyNative.ExitWatchAdd(_watch, pid, token);
                if (rc == 1)
                    return Task.FromResult(-1);
                if (rc < 0)
                    return Task.FromException<int>(
                        new PtyIoException($"exit watch registration failed (pid {pid}, errno {-rc}).", -rc));
                _byPid.Add(pid, registration);
                _byToken.Add(token, registration);
                return ObserveAsync(registration, cancellationToken);
            }
        }

        internal bool IsRegistered(int pid)
        {
            lock (_gate)
                return _byPid.ContainsKey(pid);
        }

        private long NextToken()
        {
            do
            {
                _nextToken++;
                if (_nextToken <= 0)
                    _nextToken = 1;
            }
            while (_byToken.ContainsKey(_nextToken));
            return _nextToken;
        }

        private async Task<int> ObserveAsync(Registration registration, CancellationToken cancellationToken)
        {
            Exception? failure = null;
            var status = -1;
            try
            {
                status = cancellationToken.CanBeCanceled
                    ? await registration.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false)
                    : await registration.Completion.Task.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            var cleanupFailure = Release(registration);
            if (failure is not null)
            {
                if (cleanupFailure is not null)
                    throw new AggregateException(failure, cleanupFailure);
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
            if (cleanupFailure is not null)
                throw cleanupFailure;
            return status;
        }

        private Exception? Release(Registration registration)
        {
            lock (_gate)
            {
                registration.ObserverCount--;
                if (registration.ObserverCount != 0 || !registration.NativeActive)
                    return null;
                registration.NativeActive = false;
                _byPid.Remove(registration.Pid);
                _byToken.Remove(registration.Token);
                var rc = CovePtyNative.ExitWatchRemove(_watch, registration.Token);
                return rc < 0
                    ? new PtyIoException(
                        $"exit watch removal failed (pid {registration.Pid}, errno {-rc}).",
                        -rc)
                    : null;
            }
        }

        private void RunLoop()
        {
            for (; ; )
            {
                var token = CovePtyNative.ExitWatchNext(_watch, out var status);
                if (token > 0)
                {
                    Registration? registration;
                    lock (_gate)
                    {
                        if (!_byToken.Remove(token, out registration))
                            continue;
                        _byPid.Remove(registration.Pid);
                        registration.NativeActive = false;
                    }
                    registration.Completion.TrySetResult(status);
                    continue;
                }

                List<(Registration Registration, Exception Failure)> failed = [];
                lock (_gate)
                {
                    var primaryFailure = new PtyIoException(
                        $"exit watcher loop failed (errno {-token}).",
                        (int)-token);
                    foreach (var registration in _byToken.Values)
                    {
                        registration.NativeActive = false;
                        var rc = CovePtyNative.ExitWatchRemove(_watch, registration.Token);
                        Exception failure = rc < 0
                            ? new AggregateException(
                                primaryFailure,
                                new PtyIoException(
                                    $"exit watch removal failed (pid {registration.Pid}, errno {-rc}).",
                                    -rc))
                            : primaryFailure;
                        failed.Add((registration, failure));
                    }
                    _byPid.Clear();
                    _byToken.Clear();
                }
                foreach (var entry in failed)
                    entry.Registration.Completion.TrySetException(entry.Failure);
                return;
            }
        }

        private sealed class Registration(int pid, long token)
        {
            internal int Pid { get; } = pid;
            internal long Token { get; } = token;
            internal TaskCompletionSource<int> Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal int ObserverCount { get; set; } = 1;
            internal bool NativeActive { get; set; } = true;
        }
    }
}
