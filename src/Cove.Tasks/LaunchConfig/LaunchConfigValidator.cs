namespace Cove.Tasks.LaunchConfig;

public static class LaunchConfigValidator
{
    public static LaunchConfigValidationResult Validate(LaunchConfigModel config, LaunchConfigValidationContext context)
    {
        var errors = new System.Collections.Generic.List<string>();

        if (config.Adapter is not null && !context.KnownAdapters.Contains(config.Adapter))
            errors.Add($"unknown adapter '{config.Adapter}'");

        if (config.ProfileSlug is not null && !context.KnownProfileSlugs.Contains(config.ProfileSlug))
            errors.Add($"unknown profile '{config.ProfileSlug}'");

        ValidateStatusGate(config.InProgressStatusId, "in_progress_status", context.KnownStatuses, errors);
        ValidateStatusGate(config.ReviewStatusId, "review_status", context.KnownStatuses, errors);
        ValidateStatusGate(config.CompletionStatusId, "completion_status", context.KnownStatuses, errors);

        if (config.ExecutionMode == "worktree")
        {
            if (string.IsNullOrEmpty(config.WorktreeBranchSource))
                errors.Add("worktree execution mode requires branch_source");
        }

        return new LaunchConfigValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateStatusGate(string? statusId, string field, System.Collections.Generic.IReadOnlySet<string> knownStatuses, System.Collections.Generic.List<string> errors)
    {
        if (statusId is not null && !knownStatuses.Contains(statusId))
            errors.Add($"unknown {field} '{statusId}'");
    }
}
