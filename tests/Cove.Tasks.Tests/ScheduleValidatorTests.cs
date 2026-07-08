using Cove.Tasks.Schedules;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class ScheduleValidatorTests
{
    private static System.Collections.Generic.IReadOnlySet<string> Statuses() => new System.Collections.Generic.HashSet<string> { "todo", "in-progress", "done" };
    private static readonly ICronExpander Expander = new HandRolledCronExpander();

    [Fact]
    public void Cron_ValidExpression_ComputesNextFireAt()
    {
        var p = new ScheduleSetParams("card-1", "cron", "0 9 * * *", null, null, null, null, null, null);
        var result = ScheduleValidator.Validate(p, Statuses(), Expander);
        Assert.True(result.IsValid, string.Join(", ", result.Errors));
        Assert.NotNull(result.NextFireAt);
        var next = System.DateTimeOffset.Parse(result.NextFireAt!);
        Assert.Equal(9, next.Hour);
        Assert.Equal(0, next.Minute);
    }

    [Fact]
    public void Cron_MissingCron_Rejects()
    {
        var p = new ScheduleSetParams("card-1", "cron", null, null, null, null, null, null, null);
        var result = ScheduleValidator.Validate(p, Statuses(), Expander);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("requires cron expression"));
    }

    [Fact]
    public void Cron_InvalidExpression_Rejects()
    {
        var p = new ScheduleSetParams("card-1", "cron", "not-a-cron", null, null, null, null, null, null);
        var result = ScheduleValidator.Validate(p, Statuses(), Expander);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("could not be expanded"));
    }

    [Fact]
    public void Datetime_ValidISO_ComputesNextFireAt()
    {
        var p = new ScheduleSetParams("card-1", "datetime", null, null, "2026-12-25T10:00:00Z", null, null, null, null);
        var result = ScheduleValidator.Validate(p, Statuses(), Expander);
        Assert.True(result.IsValid);
        Assert.Equal("2026-12-25T10:00:00.0000000+00:00", result.NextFireAt);
    }

    [Fact]
    public void Datetime_InvalidISO_Rejects()
    {
        var p = new ScheduleSetParams("card-1", "datetime", null, null, "not-a-date", null, null, null, null);
        var result = ScheduleValidator.Validate(p, Statuses(), Expander);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("valid ISO-8601"));
    }

    [Fact]
    public void Datetime_MissingAt_Rejects()
    {
        var p = new ScheduleSetParams("card-1", "datetime", null, null, null, null, null, null, null);
        var result = ScheduleValidator.Validate(p, Statuses(), Expander);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("requires at"));
    }

    [Fact]
    public void Immediate_NoNextFireAt()
    {
        var p = new ScheduleSetParams("card-1", "immediate", null, null, null, null, null, null, null);
        var result = ScheduleValidator.Validate(p, Statuses(), Expander);
        Assert.True(result.IsValid);
        Assert.Null(result.NextFireAt);
    }

    [Fact]
    public void UnknownTriggerKind_Rejects()
    {
        var p = new ScheduleSetParams("card-1", "hourly", null, null, null, null, null, null, null);
        var result = ScheduleValidator.Validate(p, Statuses(), Expander);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unknown trigger_kind"));
    }

    [Fact]
    public void UnknownHomeStatus_Rejects()
    {
        var p = new ScheduleSetParams("card-1", "immediate", null, null, null, null, null, null, "nonexistent");
        var result = ScheduleValidator.Validate(p, Statuses(), Expander);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unknown home_status"));
    }

    [Fact]
    public void UnknownCompletionRule_Rejects()
    {
        var p = new ScheduleSetParams("card-1", "immediate", null, null, null, "forever", null, null, null);
        var result = ScheduleValidator.Validate(p, Statuses(), Expander);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unknown completion_rule"));
    }

    [Fact]
    public void ModeResolver_NoSchedule_IsManual()
    {
        Assert.Equal("Manual", ScheduleModeResolver.DeriveMode(null));
    }

    [Fact]
    public void ModeResolver_Cron_IsScheduled()
    {
        var row = new ScheduleRow { TriggerKind = "cron" };
        Assert.Equal("Scheduled", ScheduleModeResolver.DeriveMode(row));
    }

    [Fact]
    public void ModeResolver_Immediate_IsLoop()
    {
        var row = new ScheduleRow { TriggerKind = "immediate" };
        Assert.Equal("Loop", ScheduleModeResolver.DeriveMode(row));
    }

    [Fact]
    public void ModeResolver_Paused_IsPaused()
    {
        var row = new ScheduleRow { TriggerKind = "cron", Paused = true };
        Assert.Equal("Paused", ScheduleModeResolver.DeriveMode(row));
    }
}
