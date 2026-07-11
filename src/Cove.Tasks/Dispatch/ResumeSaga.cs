using Cove.Tasks.Contracts;
using Cove.Tasks.Runs;
using Microsoft.Extensions.Logging;

namespace Cove.Tasks.Dispatch;

public sealed record ResumeResult(bool Success, string? NewSegmentId, string? Error, ResumeOutcome Outcome);

public enum ResumeOutcome
{
    Resumed,
    NoCapturedSession,
    RunNotFound,
    InvalidState,
    AdapterResumeFailed,
}

public sealed class ResumeSaga
{
    private readonly TaskService _tasks;
    private readonly ILaunchProfileResolver _profileResolver;
    private readonly INookHost _nookHost;
    private readonly IAdapterResumeLauncher _resumeLauncher;
    private readonly ILogger _logger;

    public ResumeSaga(TaskService tasks, ILaunchProfileResolver profileResolver, INookHost nookHost, IAdapterResumeLauncher resumeLauncher, ILogger logger)
    {
        _tasks = tasks;
        _profileResolver = profileResolver;
        _nookHost = nookHost;
        _resumeLauncher = resumeLauncher;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task<ResumeResult> ResumeAsync(string runId, string? nookId, string? adapterOverride)
    {
        var run = _tasks.GetRun(runId);
        if (run is null)
        {
            _logger.LogWarning("resume: run {runId} not found", runId);
            return new ResumeResult(false, null, "run not found", ResumeOutcome.RunNotFound);
        }

        if (run.State is not ("interrupted" or "completed" or "resuming"))
        {
            _logger.LogWarning("resume: run {runId} in state {state} cannot be resumed", runId, run.State);
            return new ResumeResult(false, null, $"run in state {run.State} cannot be resumed", ResumeOutcome.InvalidState);
        }

        var segments = _tasks.ListRunSegments(runId);
        var lastSegment = segments.Count > 0 ? segments[^1] : null;
        var priorSessionId = lastSegment?.AdapterSessionId;

        if (string.IsNullOrEmpty(priorSessionId))
        {
            _logger.LogWarning("resume: run {runId} has no captured adapter_session_id (default-launched or legacy)", runId);
            return new ResumeResult(false, null, "run has no captured adapter session to resume from", ResumeOutcome.NoCapturedSession);
        }

        try { await _tasks.TransitionRunAsync(runId, RunState.Resuming); }
        catch (System.InvalidOperationException ex)
        {
            return new ResumeResult(false, null, ex.Message, ResumeOutcome.InvalidState);
        }

        var card = _tasks.GetCard(run.CardId);
        if (card is null)
        {
            _logger.LogWarning("resume: card {cardId} not found for run {runId}", run.CardId, runId);
            return new ResumeResult(false, null, "card not found", ResumeOutcome.RunNotFound);
        }

        var resolution = _profileResolver.ResolveTaskProfile(run.BayId, run.CardId);
        if (resolution is null)
        {
            _logger.LogWarning("resume: profile resolution failed for card {cardId}", run.CardId);
            return new ResumeResult(false, null, "could not resolve launch profile for resume", ResumeOutcome.AdapterResumeFailed);
        }

        var adapter = adapterOverride ?? resolution.Adapter;

        var env = new System.Collections.Generic.Dictionary<string, string>(resolution.Env)
        {
            ["COVE_TASK_ID"] = card.Id,
            ["COVE_TASK_RUN_ID"] = run.Id,
        };

        if (nookId is not null)
        {
            if (!_nookHost.InjectEnv(nookId, env))
            {
                _logger.LogWarning("resume: env injection failed for nook {nookId}", nookId);
                return new ResumeResult(false, null, "env injection failed", ResumeOutcome.AdapterResumeFailed);
            }
            _nookHost.BindTaskCard(nookId, card.Id);
        }

        var resolvedCommand = resolution.ResolvedCommand ?? "";
        var resumeResult = _resumeLauncher.Resume(nookId ?? "", adapter, resolvedCommand, priorSessionId!, env);
        if (!resumeResult.Success)
        {
            _logger.LogWarning("resume: adapter resume failed for run {runId}: {error}", runId, resumeResult.Error);
            try { await _tasks.TransitionRunAsync(runId, RunState.Interrupted); }
            catch (System.InvalidOperationException) { }
            return new ResumeResult(false, null, resumeResult.Error ?? "adapter resume failed", ResumeOutcome.AdapterResumeFailed);
        }

        var newSegment = await _tasks.AddRunSegmentAsync(runId, nookId, resumeResult.AdapterSessionId);

        try { await _tasks.TransitionRunAsync(runId, RunState.Active); }
        catch (System.InvalidOperationException ex)
        {
            return new ResumeResult(false, newSegment?.Id, ex.Message, ResumeOutcome.InvalidState);
        }

        return new ResumeResult(true, newSegment?.Id, null, ResumeOutcome.Resumed);
    }
}
