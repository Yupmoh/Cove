using Cove.Tasks.Runs;
using Microsoft.Extensions.Logging;

namespace Cove.Tasks.Restart;

public sealed class RunRestorationService
{
    private readonly TaskService _tasks;
    private readonly ILogger _logger;

    public RunRestorationService(TaskService tasks, ILogger logger)
    {
        _tasks = tasks;
        _logger = logger;
    }

    public RestoredRunSummary RestoreOnStartup()
    {
        var activeRuns = _tasks.ListRuns(null, null, "active");
        var resumingRuns = _tasks.ListRuns(null, null, "resuming");
        var allNonTerminal = activeRuns.Concat(resumingRuns).ToList();

        var restored = new System.Collections.Generic.List<RestoredRun>();
        foreach (var run in allNonTerminal)
        {
            var segments = _tasks.ListRunSegments(run.Id);
            var lastSegment = segments.Count > 0 ? segments[^1] : null;

            if (lastSegment is not null && lastSegment.EndedAt is null)
            {
                _logger.LogWarning("restore: ending dead segment {segmentId} for run {runId} (nook no longer exists)", lastSegment.Id, run.Id);
                try { _tasks.EndRunSegmentAsync(lastSegment.Id).Wait(); }
                catch (System.Exception ex) { _logger.LogWarning("restore: failed to end segment {segmentId}: {error}", lastSegment.Id, ex.Message); }
            }

            try
            {
                _tasks.TransitionRunAsync(run.Id, RunState.Interrupted).Wait();
                restored.Add(new RestoredRun(run.Id, run.CardId, run.BayId, lastSegment?.NookId, lastSegment?.AdapterSessionId, RestoredRunOutcome.Interrupted));
                _logger.LogWarning("restore: run {runId} flipped to interrupted (was {prevState})", run.Id, run.State);
            }
            catch (System.InvalidOperationException ex)
            {
                _logger.LogWarning("restore: could not flip run {runId} to interrupted: {error}", run.Id, ex.Message);
                restored.Add(new RestoredRun(run.Id, run.CardId, run.BayId, lastSegment?.NookId, lastSegment?.AdapterSessionId, RestoredRunOutcome.Skipped));
            }
        }

        return new RestoredRunSummary(restored);
    }

    public async System.Threading.Tasks.Task<ResumeOnRestartResult> ResumeOnRestartAsync(RestoredRun run, System.Func<string, System.Threading.Tasks.Task<bool>>? adapterReadyCheck, System.Threading.CancellationToken ct)
    {
        if (run.AdapterSessionId is null)
        {
            _logger.LogWarning("resume-on-restart: run {runId} has no captured adapter_session_id, marking failed with Nudge", run.RunId);
            try { await _tasks.TransitionRunAsync(run.RunId, RunState.Failed); }
            catch (System.InvalidOperationException) { }
            return new ResumeOnRestartResult(run.RunId, ResumeOnRestartOutcome.FailedNoSession);
        }

        if (adapterReadyCheck is not null)
        {
            var ready = false;
            try { ready = await adapterReadyCheck(run.AdapterSessionId); }
            catch (System.Exception ex) { _logger.LogWarning("resume-on-restart: adapter-ready check failed for run {runId}: {error}", run.RunId, ex.Message); }

            if (!ready)
            {
                _logger.LogWarning("resume-on-restart: adapter not ready for run {runId}, staying interrupted", run.RunId);
                return new ResumeOnRestartResult(run.RunId, ResumeOnRestartOutcome.AdapterNotReady);
            }
        }

        try
        {
            await _tasks.TransitionRunAsync(run.RunId, RunState.Resuming);
        }
        catch (System.InvalidOperationException ex)
        {
            _logger.LogWarning("resume-on-restart: could not transition run {runId} to resuming: {error}", run.RunId, ex.Message);
            return new ResumeOnRestartResult(run.RunId, ResumeOnRestartOutcome.FailedTransition);
        }

        await _tasks.AddRunSegmentAsync(run.RunId, run.NookId, run.AdapterSessionId);

        try
        {
            await _tasks.TransitionRunAsync(run.RunId, RunState.Succeeded);
            _logger.LogWarning("resume-on-restart: run {runId} resumed successfully", run.RunId);
            return new ResumeOnRestartResult(run.RunId, ResumeOnRestartOutcome.Succeeded);
        }
        catch (System.InvalidOperationException ex)
        {
            _logger.LogWarning("resume-on-restart: run {runId} transition to succeeded failed: {error}", run.RunId, ex.Message);
            try { await _tasks.TransitionRunAsync(run.RunId, RunState.Failed); }
            catch (System.InvalidOperationException) { }
            return new ResumeOnRestartResult(run.RunId, ResumeOnRestartOutcome.FailedTransition);
        }
    }
}

public enum RestoredRunOutcome { Interrupted, Skipped }
public enum ResumeOnRestartOutcome { Succeeded, FailedNoSession, AdapterNotReady, FailedTransition }

public sealed record RestoredRun(string RunId, string CardId, string BayId, string? NookId, string? AdapterSessionId, RestoredRunOutcome Outcome);
public sealed record RestoredRunSummary(System.Collections.Generic.IReadOnlyList<RestoredRun> RestoredRuns);
public sealed record ResumeOnRestartResult(string RunId, ResumeOnRestartOutcome Outcome);
