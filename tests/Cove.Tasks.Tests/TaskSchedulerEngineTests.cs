using Cove.Tasks.Scheduler;
using Cove.Tasks.Schedules;
using Cove.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class TaskSchedulerEngineTests : TasksTestBase
{
    private static readonly System.TimeSpan HandshakeTimeout = System.TimeSpan.FromSeconds(5);

    private System.Threading.Tasks.Task<TaskService> NewSvcAsync() => CreateTaskServiceAsync("cove-sched-");

    private static System.Threading.Tasks.Task SignalMutationWithinAsync(TaskSchedulerEngine engine)
    {
        return AsyncTest.CompletesWithinAsync(
            engine.SignalMutationAsync(),
            HandshakeTimeout,
            "scheduler mutation handshake did not complete");
    }

    private static System.Threading.Tasks.Task StopWithinAsync(
        TaskSchedulerEngine engine,
        System.Threading.CancellationTokenSource cts,
        System.Threading.Tasks.Task runTask)
    {
        cts.Cancel();
        engine.Stop();
        return AsyncTest.CompletesWithinAsync(
            runTask,
            HandshakeTimeout,
            "scheduler loop did not stop");
    }

    private static async System.Threading.Tasks.Task<string> SeedCardWithCronScheduleAsync(TaskService svc, string cron, string? tz = null, System.DateTimeOffset? scheduleBaseTime = null)
    {
        var cardId = (await svc.CreateCardAsync("ws1", "sched card", "user:test", "", 1, 2, null)).Id;
        var expander = new HandRolledCronExpander();
        var baseTime = scheduleBaseTime ?? System.DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var nextFire = expander.ComputeNextFire(cron, baseTime, tz);
        var row = new ScheduleRow
        {
            CardId = cardId,
            TriggerKind = "cron",
            Cron = cron,
            Tz = tz,
            CompletionRule = "loop",
            MarkDoneBy = "agent",
            BlockOverlap = true,
            Paused = false,
            SkipNext = false,
            NextFireAt = nextFire,
        };
        await svc.UpsertScheduleAsync(row);
        return cardId;
    }

    [Fact]
    public async System.Threading.Tasks.Task CronSchedule_FiresAtDueTime_MintsBackgroundRun()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardWithCronScheduleAsync(svc, "0 9 * * *");
        var startTime = System.DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var clock = new VirtualClock(startTime);
        var engine = new TaskSchedulerEngine(svc, new HandRolledCronExpander(), clock, NullLogger.Instance);

        using var cts = new System.Threading.CancellationTokenSource();
        var runTask = engine.StartAsync(cts.Token);

        clock.AdvanceTo(System.DateTimeOffset.Parse("2026-01-01T09:01:00Z"));
        await SignalMutationWithinAsync(engine);

        var runs = svc.ListRuns(cardId, null, null);
        Assert.Single(runs);
        Assert.True(runs[0].Backgrounded);

        await StopWithinAsync(engine, cts, runTask);
    }

    [Fact]
    public async System.Threading.Tasks.Task OverlapGate_SkipsWhenActiveRunExists()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardWithCronScheduleAsync(svc, "0 9 * * *");
        await svc.CreateRunAsync(cardId, "ws1", null);

        var startTime = System.DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var clock = new VirtualClock(startTime);
        var engine = new TaskSchedulerEngine(svc, new HandRolledCronExpander(), clock, NullLogger.Instance);

        using var cts = new System.Threading.CancellationTokenSource();
        var runTask = engine.StartAsync(cts.Token);

        clock.AdvanceTo(System.DateTimeOffset.Parse("2026-01-01T09:01:00Z"));
        await SignalMutationWithinAsync(engine);

        var runs = svc.ListRuns(cardId, null, null);
        Assert.Single(runs);

        await StopWithinAsync(engine, cts, runTask);
    }

    [Fact]
    public async System.Threading.Tasks.Task SkipNext_ConsumedDoesNotFire()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardWithCronScheduleAsync(svc, "0 9 * * *");
        await svc.UpdateScheduleAsync(cardId, paused: null, skipNext: true, nextFireAt: null, lastFiredAt: null);

        var startTime = System.DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var clock = new VirtualClock(startTime);
        var engine = new TaskSchedulerEngine(svc, new HandRolledCronExpander(), clock, NullLogger.Instance);

        using var cts = new System.Threading.CancellationTokenSource();
        var runTask = engine.StartAsync(cts.Token);

        clock.AdvanceTo(System.DateTimeOffset.Parse("2026-01-01T09:01:00Z"));
        await SignalMutationWithinAsync(engine);

        var runs = svc.ListRuns(cardId, null, null);
        Assert.Empty(runs);

        var schedule = svc.GetSchedule(cardId);
        Assert.False(schedule!.SkipNext);

        await StopWithinAsync(engine, cts, runTask);
    }

    [Fact]
    public async System.Threading.Tasks.Task PastDatetimeOneShot_FiresOnceThenClears()
    {
        var svc = await NewSvcAsync();
        var cardId = (await svc.CreateCardAsync("ws1", "one-shot", "user:test", "", 1, 2, null)).Id;
        var row = new ScheduleRow
        {
            CardId = cardId,
            TriggerKind = "datetime",
            At = "2026-01-01T05:00:00Z",
            CompletionRule = "loop",
            MarkDoneBy = "agent",
            BlockOverlap = true,
            Paused = false,
            NextFireAt = "2026-01-01T05:00:00Z",
        };
        await svc.UpsertScheduleAsync(row);

        var startTime = System.DateTimeOffset.Parse("2026-01-01T06:00:00Z");
        var clock = new VirtualClock(startTime);
        var engine = new TaskSchedulerEngine(svc, new HandRolledCronExpander(), clock, NullLogger.Instance);

        using var cts = new System.Threading.CancellationTokenSource();
        var runTask = engine.StartAsync(cts.Token);

        await SignalMutationWithinAsync(engine);

        var schedule = svc.GetSchedule(cardId);
        Assert.NotNull(schedule);
        Assert.True(string.IsNullOrEmpty(schedule!.NextFireAt));

        await StopWithinAsync(engine, cts, runTask);
    }

    [Fact]
    public async System.Threading.Tasks.Task CronRetroFire_DoesNotCatchUpMissedOccurrences()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardWithCronScheduleAsync(svc, "0 9 * * *", scheduleBaseTime: System.DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var startTime = System.DateTimeOffset.Parse("2026-01-05T00:00:00Z");
        var clock = new VirtualClock(startTime);
        var engine = new TaskSchedulerEngine(svc, new HandRolledCronExpander(), clock, NullLogger.Instance);

        using var cts = new System.Threading.CancellationTokenSource();
        var runTask = engine.StartAsync(cts.Token);

        await SignalMutationWithinAsync(engine);

        var runs = svc.ListRuns(cardId, null, null);
        Assert.Empty(runs);

        var scheduleAfter = svc.GetSchedule(cardId);
        Assert.NotNull(scheduleAfter!.NextFireAt);
        var updatedFireTime = System.DateTimeOffset.Parse(scheduleAfter.NextFireAt!);
        Assert.True(updatedFireTime > startTime, $"next_fire_at {updatedFireTime} should be after clock start {startTime}");

        await StopWithinAsync(engine, cts, runTask);
    }

    [Fact]
    public async System.Threading.Tasks.Task MalformedSchedule_IsLoggedAndDoesNotStopLaterSchedule()
    {
        var svc = await NewSvcAsync();
        var malformedCardId = (await svc.CreateCardAsync("ws1", "malformed schedule", "user:test", "", 1, 2, null)).Id;
        await svc.UpsertScheduleAsync(new ScheduleRow
        {
            CardId = malformedCardId,
            TriggerKind = "datetime",
            At = "invalid",
            CompletionRule = "loop",
            MarkDoneBy = "agent",
            BlockOverlap = true,
            Paused = false,
            NextFireAt = "0000-invalid",
        });
        var validCardId = await SeedCardWithCronScheduleAsync(svc, "0 9 * * *");
        var clock = new VirtualClock(System.DateTimeOffset.Parse("2026-01-01T09:01:00Z"));
        var logger = new SchedulerCapturingLogger();
        var engine = new TaskSchedulerEngine(svc, new HandRolledCronExpander(), clock, logger);

        using var cts = new System.Threading.CancellationTokenSource();
        var runTask = engine.StartAsync(cts.Token);

        await SignalMutationWithinAsync(engine);

        var run = Assert.Single(svc.ListRuns(validCardId, null, null));
        Assert.True(run.Backgrounded);
        Assert.Contains(
            logger.Entries,
            entry =>
                entry.Level == LogLevel.Error &&
                entry.Exception is System.FormatException &&
                entry.Message.Contains(malformedCardId, System.StringComparison.Ordinal));

        await StopWithinAsync(engine, cts, runTask);
    }

    [Fact]
    public async System.Threading.Tasks.Task Stop_CancelsQueuedMutationWaiter()
    {
        var svc = await NewSvcAsync();
        var malformedCardId = (await svc.CreateCardAsync("ws1", "blocked malformed schedule", "user:test", "", 1, 2, null)).Id;
        await svc.UpsertScheduleAsync(new ScheduleRow
        {
            CardId = malformedCardId,
            TriggerKind = "datetime",
            At = "invalid",
            CompletionRule = "loop",
            MarkDoneBy = "agent",
            BlockOverlap = true,
            Paused = false,
            NextFireAt = "0000-invalid",
        });
        var dueCardId = (await svc.CreateCardAsync("ws1", "due schedule", "user:test", "", 1, 2, null)).Id;
        await svc.UpsertScheduleAsync(new ScheduleRow
        {
            CardId = dueCardId,
            TriggerKind = "immediate",
            CompletionRule = "loop",
            MarkDoneBy = "agent",
            BlockOverlap = false,
            Paused = false,
            NextFireAt = "2026-01-01T09:00:00Z",
        });

        using var loggerTimeout = new System.Threading.CancellationTokenSource(System.TimeSpan.FromSeconds(30));
        using var logger = new BlockingErrorLogger(loggerTimeout.Token);
        var clock = new VirtualClock(System.DateTimeOffset.Parse("2026-01-01T09:01:00Z"));
        var engine = new TaskSchedulerEngine(svc, new HandRolledCronExpander(), clock, logger);
        using var engineCts = new System.Threading.CancellationTokenSource();
        var runTask = System.Threading.Tasks.Task.Run(() => engine.StartAsync(engineCts.Token));

        await AsyncTest.CompletesWithinAsync(
            logger.Entered,
            HandshakeTimeout,
            "scheduler did not reach the malformed schedule log barrier");
        var pendingMutation = engine.SignalMutationAsync();
        Assert.False(pendingMutation.IsCompleted);

        engine.Stop();
        try
        {
            await Assert.ThrowsAnyAsync<System.OperationCanceledException>(
                () => AsyncTest.CompletesWithinAsync(
                    pendingMutation,
                    HandshakeTimeout,
                    "queued scheduler mutation waiter was stranded by Stop while schedule processing remained blocked"));
            Assert.True(pendingMutation.IsCanceled);
        }
        finally
        {
            logger.Release();
        }

        await AsyncTest.CompletesWithinAsync(
            runTask,
            HandshakeTimeout,
            "scheduler loop did not stop after cancellation");
    }

    [Fact]
    public async System.Threading.Tasks.Task VirtualClock_AdvanceSettlesWakeBeforeAdvancedObserversRun()
    {
        var start = System.DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var target = start.AddHours(1);
        var clock = new VirtualClock(start);
        using var timeout = new System.Threading.CancellationTokenSource(HandshakeTimeout);
        using var observerEntered = new System.Threading.ManualResetEventSlim(false);
        using var releaseObserver = new System.Threading.ManualResetEventSlim(false);
        clock.Advanced += (_, _) =>
        {
            observerEntered.Set();
            releaseObserver.Wait(timeout.Token);
        };

        var wake = clock.DelayUntil(target, timeout.Token);
        var advance = System.Threading.Tasks.Task.Run(() => clock.AdvanceTo(target), timeout.Token);

        observerEntered.Wait(timeout.Token);
        try
        {
            await AsyncTest.CompletesWithinAsync(
                wake,
                HandshakeTimeout,
                "virtual clock wake was lost behind an AdvanceTo observer");
        }
        finally
        {
            releaseObserver.Set();
        }

        await AsyncTest.CompletesWithinAsync(
            advance,
            HandshakeTimeout,
            "virtual clock AdvanceTo did not finish");
        Assert.Equal(target, clock.UtcNow);
    }

    [Fact]
    public async System.Threading.Tasks.Task FutureSchedule_FiresFromClockWakeWithoutMutationSignal()
    {
        var svc = await NewSvcAsync();
        var start = System.DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var cardId = await SeedCardWithCronScheduleAsync(svc, "0 9 * * *", scheduleBaseTime: start);
        var clock = new TrackingVirtualClock(start);
        var logger = new SchedulerCapturingLogger();
        var engine = new TaskSchedulerEngine(svc, new HandRolledCronExpander(), clock, logger);
        using var cts = new System.Threading.CancellationTokenSource();
        var runTask = engine.StartAsync(cts.Token);

        await AsyncTest.CompletesWithinAsync(
            clock.Waiting,
            HandshakeTimeout,
            "scheduler did not query the future schedule and enter its clock wait");
        clock.AdvanceTo(System.DateTimeOffset.Parse("2026-01-01T09:00:00Z"));
        await AsyncTest.CompletesWithinAsync(
            logger.BackgroundRunMinted,
            HandshakeTimeout,
            "future schedule did not fire solely from the virtual clock wake");

        var run = Assert.Single(svc.ListRuns(cardId, null, null));
        Assert.True(run.Backgrounded);

        await StopWithinAsync(engine, cts, runTask);
    }

    [Fact]
    public async System.Threading.Tasks.Task ContinueIntent_IsDurablyConsumedBeforeMutationAcknowledgement()
    {
        var svc = await NewSvcAsync();
        var now = System.DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var cardId = await SeedCardWithCronScheduleAsync(svc, "0 9 * * *", scheduleBaseTime: now);
        await svc.UpdateScheduleAsync(
            cardId,
            paused: null,
            skipNext: null,
            nextFireAt: "",
            lastFiredAt: null,
            pendingIntent: "continue");
        var engine = new TaskSchedulerEngine(svc, new HandRolledCronExpander(), new VirtualClock(now), NullLogger.Instance);
        using var cts = new System.Threading.CancellationTokenSource();
        var runTask = engine.StartAsync(cts.Token);

        await SignalMutationWithinAsync(engine);

        var completed = Assert.IsType<ScheduleRow>(svc.GetSchedule(cardId));
        Assert.Null(completed.PendingIntent);
        Assert.True(System.DateTimeOffset.Parse(completed.NextFireAt!) > now);
        await StopWithinAsync(engine, cts, runTask);
    }

    [Fact]
    public async System.Threading.Tasks.Task FinishIntent_IsDurablyConsumedBeforeMutationAcknowledgement()
    {
        var svc = await NewSvcAsync();
        var now = System.DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var cardId = await SeedCardWithCronScheduleAsync(svc, "0 9 * * *", scheduleBaseTime: now);
        await svc.UpdateScheduleAsync(
            cardId,
            paused: null,
            skipNext: null,
            nextFireAt: "",
            lastFiredAt: null,
            pendingIntent: "finish");
        var engine = new TaskSchedulerEngine(svc, new HandRolledCronExpander(), new VirtualClock(now), NullLogger.Instance);
        using var cts = new System.Threading.CancellationTokenSource();
        var runTask = engine.StartAsync(cts.Token);

        await SignalMutationWithinAsync(engine);

        Assert.Null(svc.GetSchedule(cardId));
        await StopWithinAsync(engine, cts, runTask);
    }

    [Fact]
    public async System.Threading.Tasks.Task ScheduledRun_WhenScheduleAdvanceFails_RollsBackRun()
    {
        var svc = await NewSvcAsync();
        var card = await svc.CreateCardAsync("ws1", "orphan guard", "user:test", "", 1, 2, null);

        await Assert.ThrowsAsync<System.InvalidOperationException>(
            () => svc.CreateScheduledRunAndAdvanceAsync(
                card,
                "2026-01-02T09:00:00Z",
                "2026-01-01T09:00:00Z"));

        Assert.Empty(svc.ListRuns(card.Id, null, null));
    }

    private sealed class TrackingVirtualClock : IVirtualClock
    {
        private readonly VirtualClock _inner;
        private readonly System.Threading.Tasks.TaskCompletionSource<object?> _waiting =
            new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        public TrackingVirtualClock(System.DateTimeOffset startTime) =>
            _inner = new VirtualClock(startTime);

        public System.DateTimeOffset UtcNow => _inner.UtcNow;
        public System.Threading.Tasks.Task Waiting => _waiting.Task;

        public void AdvanceTo(System.DateTimeOffset target) => _inner.AdvanceTo(target);

        public System.Threading.Tasks.Task DelayUntil(
            System.DateTimeOffset target,
            System.Threading.CancellationToken ct)
        {
            var delay = _inner.DelayUntil(target, ct);
            _waiting.TrySetResult(null);
            return delay;
        }
    }

    [TimeZoneFact("America/New_York")]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void CronosCronExpander_SpringForwardDST_ComputesCorrectFireTime()
    {
        Assert.True(TimeZoneResolver.TryResolve("America/New_York", out var springZone), "Requires America/New_York timezone data");
        var expander = new CronosCronExpander();
        var baseTime = new System.DateTimeOffset(2026, 3, 7, 12, 0, 0, System.TimeSpan.Zero);
        var result = expander.ComputeNextFire("0 9 * * *", baseTime, "America/New_York");
        Assert.NotNull(result);
        var fireTime = System.DateTimeOffset.Parse(result!);
        var fireTimeLocal = System.TimeZoneInfo.ConvertTime(fireTime, springZone);
        Assert.Equal(9, fireTimeLocal.Hour);
    }

    [TimeZoneFact("America/New_York")]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void CronosCronExpander_FallBackDST_ComputesCorrectFireTime()
    {
        Assert.True(TimeZoneResolver.TryResolve("America/New_York", out var fallZone), "Requires America/New_York timezone data");
        var expander = new CronosCronExpander();
        var baseTime = new System.DateTimeOffset(2026, 10, 31, 12, 0, 0, System.TimeSpan.Zero);
        var result = expander.ComputeNextFire("0 9 * * *", baseTime, "America/New_York");
        Assert.NotNull(result);
        var fireTime = System.DateTimeOffset.Parse(result!);
        var fireTimeLocal = System.TimeZoneInfo.ConvertTime(fireTime, fallZone);
        Assert.Equal(9, fireTimeLocal.Hour);
    }

    [Fact]
    public void CronosCronExpander_InvalidCron_ReturnsNull()
    {
        var expander = new CronosCronExpander();
        var result = expander.ComputeNextFire("not-valid", System.DateTimeOffset.UtcNow, null);
        Assert.Null(result);
    }

    [Fact]
    public void CronosCronExpander_StepExpression_ComputesCorrectFireTime()
    {
        var expander = new CronosCronExpander();
        var baseTime = new System.DateTimeOffset(2026, 1, 1, 0, 0, 0, System.TimeSpan.Zero);
        var result = expander.ComputeNextFire("*/15 * * * *", baseTime, null);
        Assert.NotNull(result);
        var fireTime = System.DateTimeOffset.Parse(result!);
        Assert.Equal(15, fireTime.Minute);
    }

    [Fact]
    public async System.Threading.Tasks.Task HasFiredThenDowntime_DoesNotRetroFireMissedOccurrences()
    {
        var svc = await NewSvcAsync();
        var cardId = (await svc.CreateCardAsync("ws1", "downtime card", "user:test", "", 1, 2, null)).Id;
        var lastFired = System.DateTimeOffset.Parse("2026-01-01T09:00:00Z");
        var staleNextFire = lastFired.AddHours(24);
        var row = new ScheduleRow
        {
            CardId = cardId,
            TriggerKind = "cron",
            Cron = "0 9 * * *",
            CompletionRule = "loop",
            MarkDoneBy = "agent",
            BlockOverlap = true,
            Paused = false,
            NextFireAt = staleNextFire.ToString("o"),
            LastFiredAt = lastFired.ToString("o"),
        };
        await svc.UpsertScheduleAsync(row);

        var startTime = staleNextFire.AddHours(48);
        var clock = new VirtualClock(startTime);
        var engine = new TaskSchedulerEngine(svc, new CronosCronExpander(), clock, NullLogger.Instance);

        using var cts = new System.Threading.CancellationTokenSource();
        var runTask = engine.StartAsync(cts.Token);

        await SignalMutationWithinAsync(engine);

        var runs = svc.ListRuns(cardId, null, null);
        Assert.Empty(runs);

        var scheduleAfter = svc.GetSchedule(cardId);
        Assert.NotNull(scheduleAfter!.NextFireAt);
        var updatedFireTime = System.DateTimeOffset.Parse(scheduleAfter.NextFireAt!);
        Assert.True(updatedFireTime > startTime, $"next_fire_at {updatedFireTime} should be after clock start {startTime}");

        await StopWithinAsync(engine, cts, runTask);
    }

    private sealed record SchedulerLogEntry(
        LogLevel Level,
        string Message,
        System.Exception? Exception);

    private sealed class SchedulerCapturingLogger : ILogger
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<SchedulerLogEntry> _entries = new();
        private readonly System.Threading.Tasks.TaskCompletionSource<object?> _backgroundRunMinted =
            new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        public System.Collections.Generic.IReadOnlyCollection<SchedulerLogEntry> Entries => _entries;
        public System.Threading.Tasks.Task BackgroundRunMinted => _backgroundRunMinted.Task;

        public System.IDisposable BeginScope<TState>(TState state) where TState : notnull =>
            NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            System.Exception? exception,
            System.Func<TState, System.Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _entries.Enqueue(new SchedulerLogEntry(logLevel, message, exception));
            if (message.Contains("minted background run", System.StringComparison.Ordinal))
                _backgroundRunMinted.TrySetResult(null);
        }
    }

    private sealed class BlockingErrorLogger : ILogger, System.IDisposable
    {
        private readonly System.Threading.CancellationToken _timeoutToken;
        private readonly System.Threading.ManualResetEventSlim _release = new(false);
        private readonly System.Threading.Tasks.TaskCompletionSource<object?> _entered =
            new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        private int _blocked;

        public BlockingErrorLogger(System.Threading.CancellationToken timeoutToken) =>
            _timeoutToken = timeoutToken;

        public System.Threading.Tasks.Task Entered => _entered.Task;

        public System.IDisposable BeginScope<TState>(TState state) where TState : notnull =>
            NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            System.Exception? exception,
            System.Func<TState, System.Exception?, string> formatter)
        {
            if (logLevel < LogLevel.Error || System.Threading.Interlocked.Exchange(ref _blocked, 1) != 0)
                return;

            _entered.TrySetResult(null);
            _release.Wait(_timeoutToken);
        }

        public void Release() => _release.Set();

        public void Dispose() => _release.Dispose();
    }
}

internal sealed class TimeZoneFactAttribute : FactAttribute
{
    public TimeZoneFactAttribute(string timeZoneId)
    {
        if (!TimeZoneResolver.TryResolve(timeZoneId, out _))
            Skip = $"Requires {timeZoneId} timezone data";
    }
}
