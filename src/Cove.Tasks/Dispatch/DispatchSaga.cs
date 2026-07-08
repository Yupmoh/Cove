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
    EnsurePane,
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
    private readonly IPaneHost _paneHost;
    private readonly IRoomService _roomService;
    private readonly IAgentLauncher _agentLauncher;
    private readonly ILogger _logger;

    public DispatchSaga(TaskService tasks, ILaunchProfileResolver profileResolver, IWorktreeService worktreeService, IPaneHost paneHost, IRoomService roomService, IAgentLauncher agentLauncher, ILogger logger)
    {
        _tasks = tasks;
        _profileResolver = profileResolver;
        _worktreeService = worktreeService;
        _paneHost = paneHost;
        _roomService = roomService;
        _agentLauncher = agentLauncher;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task<DispatchResult> LaunchAsync(string cardId, string workspaceId, string? executionModeOverride)
    {
        var card = _tasks.GetCard(cardId);
        if (card is null)
            return new DispatchResult(false, null, "card not found", DispatchStep.NotStarted);

        var config = Cove.Tasks.LaunchConfig.LaunchConfigSerializer.Deserialize(card.LaunchConfigJson);
        var executionMode = executionModeOverride ?? config?.ExecutionMode ?? "pane";

        var step = DispatchStep.ResolveConfig;
        try
        {
            var resolution = _profileResolver.ResolveTaskProfile(workspaceId, cardId);
            if (resolution is null)
            {
                _logger.LogWarning("dispatch: profile resolution failed for card {cardId}", cardId);
                return new DispatchResult(false, null, "could not resolve launch profile", step);
            }
            step = DispatchStep.MintRun;

            var launchProfileJson = config is not null ? Cove.Tasks.LaunchConfig.LaunchConfigSerializer.Serialize(config) : null;
            var run = await _tasks.CreateRunAsync(cardId, workspaceId, launchProfileJson);
            if (run is null)
                return new DispatchResult(false, null, "failed to create run", step);

            string? worktreeBranch = null;
            if (executionMode == "worktree")
            {
                step = DispatchStep.CreateWorktree;
                var branchSource = config?.WorktreeBranchSource ?? "task";
                var branchName = config?.WorktreeBranchName ?? $"COVE-{card.TaskNumber}";
                var worktree = _worktreeService.CreateAsync(workspaceId, branchSource, branchName, config?.MergeTarget);
                if (worktree is null)
                {
                    _logger.LogWarning("dispatch: worktree creation failed for card {cardId}", cardId);
                    await CompensateRunAsync(run.Id);
                    return new DispatchResult(false, run.Id, "worktree creation failed", step);
                }
                worktreeBranch = worktree.BranchName;
            }

            step = DispatchStep.EnsurePane;
            var paneResult = _paneHost.CreatePane(resolution.Adapter, 80, 24);
            if (paneResult is null)
            {
                _logger.LogWarning("dispatch: pane creation failed for card {cardId}", cardId);
                await CompensateRunAsync(run.Id);
                if (worktreeBranch is not null) _worktreeService.RemoveAsync(workspaceId, worktreeBranch);
                return new DispatchResult(false, run.Id, "pane creation failed", step);
            }

            if (executionMode == "worktree" && card.Title is not null)
            {
                var roomName = $"COVE-{card.TaskNumber} - {card.Title}";
                _roomService.CreateRoom(workspaceId, roomName, null);
            }

            step = DispatchStep.InjectEnv;
            var env = new System.Collections.Generic.Dictionary<string, string>(resolution.Env)
            {
                ["COVE_TASK_ID"] = cardId,
                ["COVE_TASK_RUN_ID"] = run.Id,
            };
            foreach (var kv in resolution.Env) env[kv.Key] = kv.Value;
            if (!_paneHost.InjectEnv(paneResult.PaneId, env))
            {
                _logger.LogWarning("dispatch: env injection failed for pane {paneId}", paneResult.PaneId);
                await CompensateRunAsync(run.Id);
                if (worktreeBranch is not null) _worktreeService.RemoveAsync(workspaceId, worktreeBranch);
                return new DispatchResult(false, run.Id, "env injection failed", step);
            }
            _paneHost.BindTaskCard(paneResult.PaneId, cardId);

            step = DispatchStep.LaunchAdapter;
            var prompt = $"{card.Title}\n\n{card.Description}";
            var launchResult = _agentLauncher.Launch(paneResult.PaneId, resolution.Adapter, resolution.ResolvedCommand ?? "", env, prompt);
            if (!launchResult.Success)
            {
                _logger.LogWarning("dispatch: adapter launch failed for card {cardId}: {error}", cardId, launchResult.Error);
                await CompensateRunAsync(run.Id);
                if (worktreeBranch is not null) _worktreeService.RemoveAsync(workspaceId, worktreeBranch);
                return new DispatchResult(false, run.Id, launchResult.Error ?? "adapter launch failed", step);
            }

            await _tasks.AddRunSegmentAsync(run.Id, paneResult.PaneId, launchResult.AdapterSessionId);

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
