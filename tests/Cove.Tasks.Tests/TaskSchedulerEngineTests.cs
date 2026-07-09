using Cove.Tasks.Scheduler;
using Cove.Tasks.Schedules;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class TaskSchedulerEngineTests
{
    private static async System.Threading.Tasks.Task<TaskService> NewSvcAsync()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-sched-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var svc = new TaskService(dir, NullLogger.Instance);
        await svc.StartAsync();
        return svc;
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
        await System.Threading.Tasks.Task.Delay(200);
        engine.SignalMutation();
        await System.Threading.Tasks.Task.Delay(200);

        var runs = svc.ListRuns(cardId, null, null);
        Assert.Single(runs);
        Assert.True(runs[0].Backgrounded);

        cts.Cancel();
        engine.Stop();
        try { await runTask; } catch { }
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
        await System.Threading.Tasks.Task.Delay(200);
        engine.SignalMutation();
        await System.Threading.Tasks.Task.Delay(200);

        var runs = svc.ListRuns(cardId, null, null);
        Assert.Single(runs);

        cts.Cancel();
        engine.Stop();
        try { await runTask; } catch { }
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
        await System.Threading.Tasks.Task.Delay(200);
        engine.SignalMutation();
        await System.Threading.Tasks.Task.Delay(200);

        var runs = svc.ListRuns(cardId, null, null);
        Assert.Empty(runs);

        var schedule = svc.GetSchedule(cardId);
        Assert.False(schedule!.SkipNext);

        cts.Cancel();
        engine.Stop();
        try { await runTask; } catch { }
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

        engine.SignalMutation();
        await System.Threading.Tasks.Task.Delay(300);

        var schedule = svc.GetSchedule(cardId);
        Assert.NotNull(schedule);
        Assert.True(string.IsNullOrEmpty(schedule!.NextFireAt));

        cts.Cancel();
        engine.Stop();
        try { await runTask; } catch { }
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

        engine.SignalMutation();
        await System.Threading.Tasks.Task.Delay(300);

        var runs = svc.ListRuns(cardId, null, null);
        Assert.Empty(runs);

        var scheduleAfter = svc.GetSchedule(cardId);
        Assert.NotNull(scheduleAfter!.NextFireAt);
        var updatedFireTime = System.DateTimeOffset.Parse(scheduleAfter.NextFireAt!);
        Assert.True(updatedFireTime > startTime, $"next_fire_at {updatedFireTime} should be after clock start {startTime}");

        cts.Cancel();
        engine.Stop();
        try { await runTask; } catch { }
    }

    [Fact]
    public void CronosCronExpander_SpringForwardDST_ComputesCorrectFireTime()
    {
        if (!TimeZoneResolver.TryResolve("America/New_York", out var springZone)) return;
        var expander = new CronosCronExpander();
        var baseTime = new System.DateTimeOffset(2026, 3, 7, 12, 0, 0, System.TimeSpan.Zero);
        var result = expander.ComputeNextFire("0 9 * * *", baseTime, "America/New_York");
        Assert.NotNull(result);
        var fireTime = System.DateTimeOffset.Parse(result!);
        var fireTimeLocal = System.TimeZoneInfo.ConvertTime(fireTime, springZone);
        Assert.Equal(9, fireTimeLocal.Hour);
    }

    [Fact]
    public void CronosCronExpander_FallBackDST_ComputesCorrectFireTime()
    {
        if (!TimeZoneResolver.TryResolve("America/New_York", out var fallZone)) return;
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

        engine.SignalMutation();
        await System.Threading.Tasks.Task.Delay(300);

        var runs = svc.ListRuns(cardId, null, null);
        Assert.Empty(runs);

        var scheduleAfter = svc.GetSchedule(cardId);
        Assert.NotNull(scheduleAfter!.NextFireAt);
        var updatedFireTime = System.DateTimeOffset.Parse(scheduleAfter.NextFireAt!);
        Assert.True(updatedFireTime > startTime, $"next_fire_at {updatedFireTime} should be after clock start {startTime}");

        cts.Cancel();
        engine.Stop();
        try { await runTask; } catch { }
    }
}
