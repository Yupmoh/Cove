using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Workspaces;

public static class WorktreeCommands
{
    [CoveCommand("cove://commands/worktree.create")]
    public static async Task<ControlResponse> WorktreeCreate(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorktreeJsonContext.Default.WorktreeCreateParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Branch) || string.IsNullOrWhiteSpace(p.Location))
            return ctx.Fail("bad_params", "parentWorkspaceId, branch and location are required");

        var workspace = await manager.CreateWorktreeAsync(p.ParentWorkspaceId, p.Branch, p.Location, p.NewBranch, p.BaseRef).ConfigureAwait(false);
        return workspace is null
            ? ctx.Fail("git_failed", "git worktree add failed or parent not found")
            : ctx.Ok(new WorktreeIdResult(workspace.Id), WorktreeJsonContext.Default.WorktreeIdResult);
    }

    [CoveCommand("cove://commands/worktree.list")]
    public static Task<ControlResponse> WorktreeList(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return Task.FromResult(ctx.Fail("no_workspaces", "workspace manager unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorktreeJsonContext.Default.WorktreeListParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "parentWorkspaceId is required"));

        var worktrees = manager.ListWorktrees(p.ParentWorkspaceId)
            .Select(w => new WorktreeSummary(w.Id, w.WorktreeBranch ?? "", w.ProjectDir))
            .ToList();
        return Task.FromResult(ctx.Ok(new WorktreeListResult(worktrees), WorktreeJsonContext.Default.WorktreeListResult));
    }

    [CoveCommand("cove://commands/worktree.remove")]
    public static async Task<ControlResponse> WorktreeRemove(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorktreeJsonContext.Default.WorktreeRemoveParams) is not { } p)
            return ctx.Fail("bad_params", "worktreeWorkspaceId is required");

        return await manager.RemoveWorktreeAsync(p.WorktreeWorkspaceId, p.Force).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("git_failed", "git worktree remove failed or not a worktree");
    }

    [CoveCommand("cove://commands/worktree.orphans")]
    public static async Task<ControlResponse> WorktreeOrphans(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorktreeJsonContext.Default.WorktreeListParams) is not { } p)
            return ctx.Fail("bad_params", "parentWorkspaceId is required");

        var orphans = await manager.WorktreeOrphansAsync(p.ParentWorkspaceId).ConfigureAwait(false);
        return ctx.Ok(new WorktreeOrphansResult(orphans), WorktreeJsonContext.Default.WorktreeOrphansResult);
    }

    [CoveCommand("cove://commands/worktree.prune")]
    public static async Task<ControlResponse> WorktreePrune(EngineDispatchContext ctx)
    {
        if (ctx.Workspaces is not { } manager)
            return ctx.Fail("no_workspaces", "workspace manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorktreeJsonContext.Default.WorktreeListParams) is not { } p)
            return ctx.Fail("bad_params", "parentWorkspaceId is required");

        return await manager.PruneWorktreesAsync(p.ParentWorkspaceId).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("git_failed", "git worktree prune failed or parent not found");
    }
}

public sealed record WorktreeCreateParams(string ParentWorkspaceId, string Branch, string Location, bool NewBranch = true, string? BaseRef = null);
public sealed record WorktreeListParams(string ParentWorkspaceId);
public sealed record WorktreeRemoveParams(string WorktreeWorkspaceId, bool Force = true);
public sealed record WorktreeIdResult(string WorkspaceId);
public sealed record WorktreeSummary(string WorkspaceId, string Branch, string ProjectDir);
public sealed record WorktreeListResult(IReadOnlyList<WorktreeSummary> Worktrees);
public sealed record WorktreeOrphansResult(IReadOnlyList<string> Paths);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WorktreeCreateParams))]
[JsonSerializable(typeof(WorktreeListParams))]
[JsonSerializable(typeof(WorktreeRemoveParams))]
[JsonSerializable(typeof(WorktreeIdResult))]
[JsonSerializable(typeof(WorktreeListResult))]
[JsonSerializable(typeof(WorktreeOrphansResult))]
public sealed partial class WorktreeJsonContext : JsonSerializerContext { }
