using System.Threading.Channels;
using Cove.Tasks.Runs;
using Microsoft.Extensions.Logging;

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
    private System.DateTimeOffset _now;
    private readonly System.Threading.ManualResetEventSlim _signal = new(false);
    private System.DateTimeOffset? _wakeAt;

    public VirtualClock(System.DateTimeOffset startTime) => _now = startTime;

    public System.DateTimeOffset UtcNow => _now;
    public event System.EventHandler? Advanced;

    public void AdvanceTo(System.DateTimeOffset target)
    {
        if (target <= _now) return;
        _now = target;
        Advanced?.Invoke(this, System.EventArgs.Empty);
        if (_wakeAt is not null && target >= _wakeAt.Value)
            _signal.Set();
    }

    public System.Threading.Tasks.Task DelayUntil(System.DateTimeOffset target, System.Threading.CancellationToken ct)
    {
        _wakeAt = target;
        if (target <= _now) return System.Threading.Tasks.Task.CompletedTask;
        _signal.Reset();
        return System.Threading.Tasks.Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested && _now < target)
            {
                _signal.Wait(100, ct);
                _signal.Reset();
            }
        }, ct);
    }
}

public sealed class TaskSchedulerEngine
{
    private readonly TaskService _tasks;
    private readonly Schedules.ICronExpander _cronExpander;
    private readonly IVirtualClock _clock;
    private readonly ILogger _logger;
    private readonly Channel<bool> _signal;
    private System.Threading.CancellationTokenSource? _cts;

    public TaskSchedulerEngine(TaskService tasks, Schedules.ICronExpander cronExpander, IVirtualClock clock, ILogger logger)
    {
        _tasks = tasks;
        _cronExpander = cronExpander;
        _clock = clock;
        _logger = logger;
        _signal = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    }

    public System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken ct)
    {
        _cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
        return RunLoopAsync(_cts.Token);
    }

    public void SignalMutation() => _signal.Writer.TryWrite(true);

    private async System.Threading.Tasks.Task RunLoopAsync(System.Threading.CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var due = _tasks.ListDueSchedules(_clock.UtcNow);

            foreach (var schedule in due)
            {
                try { await ProcessDueScheduleAsync(schedule, ct); }
                catch (System.Exception ex) { _logger.LogWarning("scheduler: error processing schedule for card {cardId}: {error}", schedule.CardId, ex.Message); }
            }

            var nextDue = ComputeNextDueTime();
            if (nextDue is { } wake)
            {
                try { await _clock.DelayUntil(wake, ct); }
                catch (System.Threading.Tasks.TaskCanceledException) { return; }
                continue;
            }

            try { await _signal.Reader.ReadAsync(ct); }
            catch (System.Threading.Tasks.TaskCanceledException) { return; }
            catch (System.Threading.Channels.ChannelClosedException) { return; }
        }
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

        var launchProfileJson = card.LaunchConfigJson;
        var run = await _tasks.CreateRunAsync(card.Id, card.WorkspaceId, launchProfileJson, backgrounded: true);
        _logger.LogWarning("scheduler: minted background run {runId} for card {cardId}", run?.Id, card.Id);

        var nextFire = ComputeNextFire(schedule, now);
        await _tasks.UpdateScheduleAsync(schedule.CardId, paused: null, skipNext: null, nextFireAt: nextFire, lastFiredAt: now.ToString("o"));
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
        foreach (var s in _tasks.ListDueSchedules(now))
        {
            if (s.NextFireAt is not null)
            {
                var fire = System.DateTimeOffset.Parse(s.NextFireAt);
                if (earliest is null || fire < earliest) earliest = fire;
            }
        }
        return earliest;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _signal.Writer.TryComplete();
    }
}
