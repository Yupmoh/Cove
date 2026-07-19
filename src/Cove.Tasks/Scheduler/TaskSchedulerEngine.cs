using System.Threading.Channels;
using Cove.Tasks.Runs;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Tasks.Scheduler;

public interface IVirtualClock
{
    System.DateTimeOffset UtcNow { get; }
    System.Threading.Tasks.Task DelayUntil(System.DateTimeOffset target, System.Threading.CancellationToken ct);
}

public sealed class SystemClock : IVirtualClock
{
    public System.DateTimeOffset UtcNow => System.DateTimeOffset.UtcNow;
    public async System.Threading.Tasks.Task DelayUntil(System.DateTimeOffset target, System.Threading.CancellationToken ct)
    {
        var delta = target - System.DateTimeOffset.UtcNow;
        if (delta <= System.TimeSpan.Zero) return;
        try { await System.Threading.Tasks.Task.Delay(delta, ct); }
        catch (System.Threading.Tasks.TaskCanceledException) { }
    }
}

public sealed class VirtualClock : IVirtualClock
{
    private readonly object _gate = new();
    private System.DateTimeOffset _now;
    private System.DateTimeOffset? _wakeAt;
    private System.Threading.Tasks.TaskCompletionSource<object?>? _wake;

    public VirtualClock(System.DateTimeOffset startTime) => _now = startTime;

    public System.DateTimeOffset UtcNow
    {
        get
        {
            lock (_gate)
                return _now;
        }
    }

    public event System.EventHandler? Advanced;

    public void AdvanceTo(System.DateTimeOffset target)
    {
        System.Threading.Tasks.TaskCompletionSource<object?>? wake = null;
        lock (_gate)
        {
            if (target <= _now)
                return;

            _now = target;
            if (_wakeAt is not null && target >= _wakeAt.Value)
            {
                wake = _wake;
                _wake = null;
                _wakeAt = null;
            }
        }

        wake?.TrySetResult(null);
        Advanced?.Invoke(this, System.EventArgs.Empty);
    }

    public async System.Threading.Tasks.Task DelayUntil(System.DateTimeOffset target, System.Threading.CancellationToken ct)
    {
        System.Threading.Tasks.TaskCompletionSource<object?> wake;
        lock (_gate)
        {
            if (target <= _now)
                return;

            wake = new System.Threading.Tasks.TaskCompletionSource<object?>(
                System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
            _wakeAt = target;
            _wake = wake;
        }

        using var registration = ct.Register(() => wake.TrySetCanceled(ct));
        try
        {
            await wake.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                if (System.Object.ReferenceEquals(_wake, wake))
                {
                    _wake = null;
                    _wakeAt = null;
                }
            }
        }
    }
}

public interface IScheduleMutationAcknowledger
{
    System.Threading.Tasks.Task SignalMutationAsync(
        System.Threading.CancellationToken ct = default);
}

public sealed class TaskSchedulerEngine : IScheduleMutationAcknowledger
{
    private readonly TaskService _tasks;
    private readonly Schedules.ICronExpander _cronExpander;
    private readonly IVirtualClock _clock;
    private readonly ILogger _logger;
    private readonly Channel<System.Threading.Tasks.TaskCompletionSource<object?>> _signal;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        System.Threading.Tasks.TaskCompletionSource<object?>,
        byte> _pendingWaiters = new();
    private System.Threading.CancellationTokenSource? _cts;
    private int _stopped;

    public TaskSchedulerEngine(TaskService tasks, Schedules.ICronExpander cronExpander, IVirtualClock clock, ILogger logger)
    {
        _tasks = tasks;
        _cronExpander = cronExpander;
        _clock = clock;
        _logger = logger;
        _signal = Channel.CreateUnbounded<System.Threading.Tasks.TaskCompletionSource<object?>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    }

    public System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken ct)
    {
        _cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (System.Threading.Volatile.Read(ref _stopped) != 0)
            _cts.Cancel();
        return RunLoopAsync(_cts.Token);
    }

    public async System.Threading.Tasks.Task SignalMutationAsync(System.Threading.CancellationToken ct = default)
    {
        if (System.Threading.Volatile.Read(ref _stopped) != 0)
            throw new System.InvalidOperationException("The scheduler is stopped.");

        var completion = new System.Threading.Tasks.TaskCompletionSource<object?>(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingWaiters.TryAdd(completion, 0);

        if (System.Threading.Volatile.Read(ref _stopped) != 0)
        {
            completion.TrySetCanceled(_cts?.Token ?? new System.Threading.CancellationToken(true));
        }
        else if (!_signal.Writer.TryWrite(completion))
        {
            _pendingWaiters.TryRemove(completion, out _);
            throw new System.InvalidOperationException("The scheduler is stopped.");
        }

        using var registration = ct.Register(() => completion.TrySetCanceled(ct));
        try
        {
            await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            _pendingWaiters.TryRemove(completion, out _);
        }
    }

    private async System.Threading.Tasks.Task RunLoopAsync(System.Threading.CancellationToken ct)
    {
        List<System.Threading.Tasks.TaskCompletionSource<object?>>? iterationWaiters = null;
        System.Exception? terminalException = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var now = _clock.UtcNow;
                await ConsumePendingIntentsAsync(now, ct);
                var active = _tasks.ListActiveSchedules();

                foreach (var schedule in active)
                {
                    try
                    {
                        var fireAt = System.DateTimeOffset.Parse(schedule.NextFireAt!);
                        if (fireAt > now)
                            continue;

                        await ProcessDueScheduleAsync(schedule, ct);
                    }
                    catch (System.OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (System.Exception exception)
                    {
                        _logger.ScheduleProcessingFailed(schedule.CardId, exception);
                    }
                }

                if (iterationWaiters is not null)
                {
                    foreach (var waiter in iterationWaiters)
                        waiter.TrySetResult(null);
                    iterationWaiters = null;
                }

                var nextDue = ComputeNextDueTime();
                if (nextDue is { } wake)
                {
                    using var waitCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
                    var delayTask = _clock.DelayUntil(wake, waitCts.Token);
                    var signalTask = _signal.Reader.ReadAsync(waitCts.Token).AsTask();
                    await System.Threading.Tasks.Task.WhenAny(delayTask, signalTask);
                    waitCts.Cancel();

                    try
                    {
                        await System.Threading.Tasks.Task.WhenAll(delayTask, signalTask).ConfigureAwait(false);
                    }
                    catch (System.OperationCanceledException) when (waitCts.IsCancellationRequested)
                    {
                    }

                    if (signalTask.IsCompletedSuccessfully)
                    {
                        iterationWaiters = [signalTask.Result];
                        while (_signal.Reader.TryRead(out var additionalSignal))
                            iterationWaiters.Add(additionalSignal);
                    }

                    continue;
                }

                var signal = await _signal.Reader.ReadAsync(ct);
                iterationWaiters = [signal];
                while (_signal.Reader.TryRead(out var additionalSignal))
                    iterationWaiters.Add(additionalSignal);
            }
        }
        catch (System.OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (System.Threading.Channels.ChannelClosedException)
        {
        }
        catch (System.Exception exception)
        {
            terminalException = exception;
            throw;
        }
        finally
        {
            _signal.Writer.TryComplete(terminalException);
            if (terminalException is null)
                CancelWaiters(iterationWaiters, ct);
            else
                FaultWaiters(iterationWaiters, terminalException);
        }
    }

    private void CancelWaiters(
        List<System.Threading.Tasks.TaskCompletionSource<object?>>? iterationWaiters,
        System.Threading.CancellationToken ct)
    {
        if (iterationWaiters is not null)
        {
            foreach (var waiter in iterationWaiters)
                waiter.TrySetCanceled(ct);
        }

        while (_signal.Reader.TryRead(out var waiter))
            waiter.TrySetCanceled(ct);
    }

    private void FaultWaiters(
        List<System.Threading.Tasks.TaskCompletionSource<object?>>? iterationWaiters,
        System.Exception exception)
    {
        if (iterationWaiters is not null)
        {
            foreach (var waiter in iterationWaiters)
                waiter.TrySetException(exception);
        }

        while (_signal.Reader.TryRead(out var waiter))
            waiter.TrySetException(exception);
    }

    private async System.Threading.Tasks.Task ProcessDueScheduleAsync(Schedules.ScheduleRow schedule, System.Threading.CancellationToken ct)
    {
        var now = _clock.UtcNow;

        if (schedule.SkipNext)
        {
            _logger.LogWarning("scheduler: skip_next consumed for card {cardId}", schedule.CardId);
            var nextAfterSkip = ComputeNextFire(schedule, now);
            await _tasks.UpdateScheduleAsync(schedule.CardId, paused: null, skipNext: false, nextFireAt: nextAfterSkip, lastFiredAt: now.ToString("o"));
            return;
        }

        if (schedule.TriggerKind == "cron" && schedule.NextFireAt is not null)
        {
            var dueTime = System.DateTimeOffset.Parse(schedule.NextFireAt);
            var staleThreshold = now.AddMinutes(-1);
            if (dueTime < staleThreshold)
            {
                _logger.LogWarning("scheduler: no-retro-fire catch-up for card {cardId} (missed occurrence at {dueTime} re-armed)", schedule.CardId, dueTime);
                var reArmedFire = ComputeNextFire(schedule, now);
                await _tasks.UpdateScheduleAsync(schedule.CardId, paused: null, skipNext: null, nextFireAt: reArmedFire, lastFiredAt: null);
                return;
            }
        }
        if (schedule.BlockOverlap && _tasks.HasActiveRun(schedule.CardId))
        {
            _logger.LogWarning("scheduler: overlap gate skipped card {cardId} (active run exists)", schedule.CardId);
            var nextAfterSkip = ComputeNextFire(schedule, now);
            await _tasks.UpdateScheduleAsync(schedule.CardId, paused: null, skipNext: null, nextFireAt: nextAfterSkip, lastFiredAt: null);
            return;
        }

        if (schedule.TriggerKind == "datetime" && schedule.NextFireAt is not null)
        {
            var fireTime = System.DateTimeOffset.Parse(schedule.NextFireAt);
            if (fireTime <= now)
            {
                _logger.LogWarning("scheduler: past-datetime one-shot fired for card {cardId}", schedule.CardId);
                await _tasks.UpdateScheduleAsync(schedule.CardId, paused: null, skipNext: null, nextFireAt: "", lastFiredAt: now.ToString("o"));
                return;
            }
        }

        if (schedule.TriggerKind == "cron" && schedule.LastFiredAt is not null && schedule.NextFireAt is not null)
        {
            var lastFire = System.DateTimeOffset.Parse(schedule.LastFiredAt);
            var parsedNextFire = System.DateTimeOffset.Parse(schedule.NextFireAt);
            if (parsedNextFire <= lastFire)
            {
                var nextFire2 = ComputeNextFire(schedule, now);
                await _tasks.UpdateScheduleAsync(schedule.CardId, paused: null, skipNext: null, nextFireAt: nextFire2, lastFiredAt: null);
                return;
            }
        }

        var card = _tasks.GetCard(schedule.CardId);
        if (card is null)
        {
            _logger.LogWarning("scheduler: card {cardId} not found for due schedule", schedule.CardId);
            await _tasks.DeleteScheduleAsync(schedule.CardId);
            return;
        }

        var nextFire = ComputeNextFire(schedule, now);
        var run = await _tasks.CreateScheduledRunAndAdvanceAsync(card, nextFire, now.ToString("o"));
        _logger.LogWarning("scheduler: minted background run {runId} for card {cardId}", run?.Id, card.Id);
    }

    private async System.Threading.Tasks.Task ConsumePendingIntentsAsync(
        System.DateTimeOffset now,
        System.Threading.CancellationToken ct)
    {
        foreach (var schedule in _tasks.ListPendingSchedules())
        {
            ct.ThrowIfCancellationRequested();
            if (schedule.PendingIntent == "continue")
            {
                var nextFire = ComputeNextFire(schedule, now);
                await _tasks.CompleteScheduleIntentAsync(schedule.CardId, nextFire);
                continue;
            }

            if (schedule.PendingIntent == "finish")
            {
                await _tasks.DeleteScheduleAsync(schedule.CardId);
                continue;
            }

            _logger.LogWarning(
                "scheduler: unknown pending intent {intent} for card {cardId}",
                schedule.PendingIntent,
                schedule.CardId);
        }
    }

    private string? ComputeNextFire(Schedules.ScheduleRow schedule, System.DateTimeOffset now)
    {
        if (schedule.TriggerKind == "immediate") return null;
        if (schedule.TriggerKind == "datetime") return "";
        if (schedule.TriggerKind == "cron") return _cronExpander.ComputeNextFire(schedule.Cron ?? "", now, schedule.Tz);
        return null;
    }

    private System.DateTimeOffset? ComputeNextDueTime()
    {
        var now = _clock.UtcNow;
        System.DateTimeOffset? earliest = null;
        foreach (var s in _tasks.ListActiveSchedules())
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(s.NextFireAt))
                {
                    var fire = System.DateTimeOffset.Parse(s.NextFireAt);
                    if (fire > now && (earliest is null || fire < earliest))
                        earliest = fire;
                }
            }
            catch (System.Exception exception)
            {
                _logger.ScheduleNextDueFailed(s.CardId, exception);
            }
        }
        return earliest;
    }

    public void Stop()
    {
        if (System.Threading.Interlocked.Exchange(ref _stopped, 1) != 0)
            return;

        var cts = _cts;
        cts?.Cancel();
        _signal.Writer.TryComplete();
        var cancellationToken = cts?.Token ?? new System.Threading.CancellationToken(true);
        foreach (var waiter in _pendingWaiters.Keys)
            waiter.TrySetCanceled(cancellationToken);
    }
}

internal static partial class SchedulerLog
{
    [ZLoggerMessage(LogLevel.Error, "scheduler schedule processing failed card={cardId}; continuing")]
    public static partial void ScheduleProcessingFailed(this ILogger logger, string cardId, System.Exception exception);

    [ZLoggerMessage(LogLevel.Error, "scheduler next-due computation failed card={cardId}; continuing")]
    public static partial void ScheduleNextDueFailed(this ILogger logger, string cardId, System.Exception exception);
}
