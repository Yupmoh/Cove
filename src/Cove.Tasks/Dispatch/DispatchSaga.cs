using Cove.Tasks.Contracts;
using Cove.Tasks.Runs;
using Cove.Tasks.Store;
using Microsoft.Extensions.Logging;

namespace Cove.Tasks.Dispatch;

public enum DispatchStep
{
    NotStarted,
    ResolveConfig,
    MintRun,
    CreateWorktree,
    EnsureNook,
    InjectEnv,
    LaunchAdapter,
    MoveStatus,
    Completed,
}

public sealed record DispatchResult(bool Success, string? RunId, string? Error, DispatchStep ReachedStep);

public sealed class DispatchSaga
{
    private readonly TaskService _tasks;
    private readonly ILaunchProfileResolver _profileResolver;
    private readonly IWorktreeService _worktreeService;
    private readonly INookHost _nookHost;
    private readonly IShoreService _shoreService;
    private readonly IAgentLauncher _agentLauncher;
    private readonly ILogger _logger;

    public DispatchSaga(TaskService tasks, ILaunchProfileResolver profileResolver, IWorktreeService worktreeService, INookHost nookHost, IShoreService shoreService, IAgentLauncher agentLauncher, ILogger logger)
    {
        _tasks = tasks;
        _profileResolver = profileResolver;
        _worktreeService = worktreeService;
        _nookHost = nookHost;
        _shoreService = shoreService;
        _agentLauncher = agentLauncher;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task<DispatchResult> LaunchAsync(string cardId, string bayId, string? executionModeOverride)
    {
        var card = _tasks.GetCard(cardId);
        if (card is null)
            return new DispatchResult(false, null, "card not found", DispatchStep.NotStarted);

        var config = Cove.Tasks.LaunchConfig.LaunchConfigSerializer.Deserialize(card.LaunchConfigJson);
        var executionMode = executionModeOverride ?? config?.ExecutionMode ?? "nook";

        var step = DispatchStep.ResolveConfig;
        try
        {
            var resolution = _profileResolver.ResolveTaskProfile(bayId, cardId);
            if (resolution is null)
            {
                _logger.LogWarning("dispatch: profile resolution failed for card {cardId}", cardId);
                return new DispatchResult(false, null, "could not resolve launch profile", step);
            }
            step = DispatchStep.MintRun;

            var launchProfileJson = config is not null ? Cove.Tasks.LaunchConfig.LaunchConfigSerializer.Serialize(config) : null;
            var run = await _tasks.CreateRunAsync(cardId, bayId, launchProfileJson, reviewStatusId: config?.ReviewStatusId, completionStatusId: config?.CompletionStatusId);
            if (run is null)
                return new DispatchResult(false, null, "failed to create run", step);

            string? worktreeBranch = null;
            if (executionMode == "worktree")
            {
                step = DispatchStep.CreateWorktree;
                var branchSource = config?.WorktreeBranchSource ?? "task";
                var branchName = config?.WorktreeBranchName ?? $"COVE-{card.TaskNumber}";
                var worktree = _worktreeService.CreateAsync(bayId, branchSource, branchName, config?.MergeTarget);
                if (worktree is null)
                {
                    _logger.LogWarning("dispatch: worktree creation failed for card {cardId}", cardId);
                    await CompensateRunAsync(run.Id);
                    return new DispatchResult(false, run.Id, "worktree creation failed", step);
                }
                worktreeBranch = worktree.BranchName;
            }

            step = DispatchStep.EnsureNook;
            var nookResult = _nookHost.CreateNook(resolution.Adapter, 80, 24);
            if (nookResult is null)
            {
                _logger.LogWarning("dispatch: nook creation failed for card {cardId}", cardId);
                await CompensateRunAsync(run.Id);
                if (worktreeBranch is not null) _worktreeService.RemoveAsync(bayId, worktreeBranch);
                return new DispatchResult(false, run.Id, "nook creation failed", step);
            }

            if (executionMode == "worktree" && card.Title is not null)
            {
                var shoreName = $"COVE-{card.TaskNumber} - {card.Title}";
                _shoreService.CreateShore(bayId, shoreName, null);
            }

            step = DispatchStep.InjectEnv;
            var env = new System.Collections.Generic.Dictionary<string, string>(resolution.Env)
            {
                ["COVE_TASK_ID"] = cardId,
                ["COVE_TASK_RUN_ID"] = run.Id,
            };
            foreach (var kv in resolution.Env) env[kv.Key] = kv.Value;
            if (!_nookHost.InjectEnv(nookResult.NookId, env))
            {
                _logger.LogWarning("dispatch: env injection failed for nook {nookId}", nookResult.NookId);
                await CompensateRunAsync(run.Id);
                if (worktreeBranch is not null) _worktreeService.RemoveAsync(bayId, worktreeBranch);
                return new DispatchResult(false, run.Id, "env injection failed", step);
            }
            _nookHost.BindTaskCard(nookResult.NookId, cardId);

            step = DispatchStep.LaunchAdapter;
            var prompt = $"{card.Title}\n\n{card.Description}";
            var launchResult = _agentLauncher.Launch(nookResult.NookId, resolution.Adapter, resolution.ResolvedCommand ?? "", env, prompt);
            if (!launchResult.Success)
            {
                _logger.LogWarning("dispatch: adapter launch failed for card {cardId}: {error}", cardId, launchResult.Error);
                await CompensateRunAsync(run.Id);
                if (worktreeBranch is not null) _worktreeService.RemoveAsync(bayId, worktreeBranch);
                return new DispatchResult(false, run.Id, launchResult.Error ?? "adapter launch failed", step);
            }

            await _tasks.AddRunSegmentAsync(run.Id, nookResult.NookId, launchResult.AdapterSessionId);

            step = DispatchStep.MoveStatus;
            var inProgressStatus = config?.InProgressStatusId ?? "in-progress";
            card.StatusId = inProgressStatus;
            card.CurrentPrimaryRunId = run.Id;
            await _tasks.UpdateCardAsync(card);

            return new DispatchResult(true, run.Id, null, DispatchStep.Completed);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning("dispatch: saga failed at step {step} for card {cardId}: {error}", step, cardId, ex.Message);
            return new DispatchResult(false, null, ex.Message, step);
        }
    }

    private async System.Threading.Tasks.Task CompensateRunAsync(string runId)
    {
        try { await _tasks.TransitionRunAsync(runId, RunState.Cancelled); }
        catch (System.InvalidOperationException) { }
    }
}
