namespace Cove.Tasks.Schedules;

public sealed record ScheduleValidationResult(bool IsValid, System.Collections.Generic.IReadOnlyList<string> Errors, string? NextFireAt);

public sealed record ScheduleSetParams(
    string CardId,
    string TriggerKind,
    string? Cron,
    string? Tz,
    string? At,
    string? CompletionRule,
    string? MarkDoneBy,
    bool? BlockOverlap,
    string? HomeStatusId);

public static class ScheduleValidator
{
    public static ScheduleValidationResult Validate(ScheduleSetParams p, System.Collections.Generic.IReadOnlySet<string> knownStatuses, ICronExpander? cronExpander = null)
    {
        var errors = new System.Collections.Generic.List<string>();

        if (p.TriggerKind is not ("immediate" or "datetime" or "cron"))
            errors.Add($"unknown trigger_kind '{p.TriggerKind}'");

        if (p.CompletionRule is not (null or "terminal" or "loop" or "respawn"))
            errors.Add($"unknown completion_rule '{p.CompletionRule}'");

        if (p.MarkDoneBy is not (null or "agent" or "review"))
            errors.Add($"unknown mark_done_by '{p.MarkDoneBy}'");

        if (p.HomeStatusId is not null && !knownStatuses.Contains(p.HomeStatusId))
            errors.Add($"unknown home_status '{p.HomeStatusId}'");

        if (p.TriggerKind == "cron" && string.IsNullOrWhiteSpace(p.Cron))
            errors.Add("cron trigger_kind requires cron expression");

        if (p.TriggerKind == "datetime" && string.IsNullOrWhiteSpace(p.At))
            errors.Add("datetime trigger_kind requires at");

        string? nextFireAt = null;

        if (errors.Count == 0)
        {
            nextFireAt = NextFireAtCalculator.Compute(p.TriggerKind, p.Cron, p.At, p.Tz, cronExpander);
            if (p.TriggerKind == "datetime" && nextFireAt is null)
                errors.Add("at is not a valid ISO-8601 datetime");
            if (p.TriggerKind == "cron" && nextFireAt is null)
                errors.Add("cron expression could not be expanded to a next fire time");
        }

        return new ScheduleValidationResult(errors.Count == 0, errors, nextFireAt);
    }
}

public static class NextFireAtCalculator
{
    public static string? Compute(string triggerKind, string? cron, string? at, string? tz, ICronExpander? cronExpander = null)
    {
        return triggerKind switch
        {
            "immediate" => null,
            "datetime" => ParseDateTime(at),
            "cron" => cronExpander?.ComputeNextFire(cron ?? "", System.DateTimeOffset.UtcNow, tz),
            _ => null,
        };
    }

    private static string? ParseDateTime(string? at)
    {
        if (string.IsNullOrWhiteSpace(at))
            return null;
        if (System.DateTimeOffset.TryParse(at, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var dto))
            return dto.ToString("o");
        return null;
    }
}

public static class ScheduleModeResolver
{
    public static string DeriveMode(ScheduleRow? schedule)
    {
        if (schedule is null)
            return "Manual";
        if (schedule.Paused)
            return "Paused";
        return schedule.TriggerKind switch
        {
            "immediate" => "Loop",
            "cron" => "Scheduled",
            "datetime" => "Repeat",
            _ => "Manual",
        };
    }
}
