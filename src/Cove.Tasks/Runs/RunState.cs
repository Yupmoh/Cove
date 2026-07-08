namespace Cove.Tasks.Runs;

public enum RunState
{
    Active,
    Interrupted,
    Completed,
    Cancelled,
    Resuming,
    Succeeded,
    Failed,
}

public static class RunStateTransitions
{
    private static readonly System.Collections.Generic.Dictionary<RunState, System.Collections.Generic.HashSet<RunState>> Allowed =
        new()
        {
            [RunState.Active] = [RunState.Interrupted, RunState.Completed, RunState.Cancelled, RunState.Resuming],
            [RunState.Interrupted] = [RunState.Resuming, RunState.Cancelled, RunState.Failed],
            [RunState.Resuming] = [RunState.Active, RunState.Succeeded, RunState.Failed, RunState.Cancelled, RunState.Interrupted],
            [RunState.Completed] = [RunState.Resuming],
            [RunState.Cancelled] = [],
            [RunState.Succeeded] = [],
            [RunState.Failed] = [],
        };

    public static bool IsValid(RunState from, RunState to)
        => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
}
