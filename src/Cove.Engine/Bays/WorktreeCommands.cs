using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Bays;

public static class WorktreeCommands
{
    [CoveCommand("cove://commands/worktree.create")]
    public static async Task<ControlResponse> WorktreeCreate(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorktreeJsonContext.Default.WorktreeCreateParams) is not { } p
            || string.IsNullOrWhiteSpace(p.ParentBayId)
            || string.IsNullOrWhiteSpace(p.Branch))
            return ctx.Fail("bad_params", "parentBayId and branch are required");

        var parent = manager.Get(p.ParentBayId);
        if (parent is null)
            return ctx.Fail("not_found", "parent bay not found");

        var location = p.Location;
        if (string.IsNullOrWhiteSpace(location))
        {
            var pattern = ctx.Config?.GetWorktreeDefaultLocationPattern() ?? "";
            if (string.IsNullOrWhiteSpace(pattern))
                return ctx.Fail("bad_params", "location is required (no worktree.defaultLocationPattern configured)");
            var repoName = WorktreePattern.DeriveRepoName(parent.State.ProjectDir);
            location = WorktreePattern.Expand(pattern, repoName, p.Branch);
        }
        location = WorktreePattern.ResolveLocation(location, parent.State.ProjectDir);

        var bay = await manager.CreateWorktreeAsync(p.ParentBayId, p.Branch, location, p.NewBranch, p.BaseRef).ConfigureAwait(false);
        return bay is null
            ? ctx.Fail("git_failed", "git worktree add failed or parent not found")
            : ctx.Ok(new WorktreeIdResult(bay.Id), WorktreeJsonContext.Default.WorktreeIdResult);
    }

    [CoveCommand("cove://commands/worktree.list")]
    public static Task<ControlResponse> WorktreeList(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorktreeJsonContext.Default.WorktreeListParams) is not { } p)
            return Task.FromResult(ctx.Fail("bad_params", "parentBayId is required"));

        var worktrees = manager.ListWorktrees(p.ParentBayId)
            .Select(w => new WorktreeSummary(w.Id, w.WorktreeBranch ?? "", w.ProjectDir))
            .ToList();
        return Task.FromResult(ctx.Ok(new WorktreeListResult(worktrees), WorktreeJsonContext.Default.WorktreeListResult));
    }

    [CoveCommand("cove://commands/worktree.remove")]
    public static async Task<ControlResponse> WorktreeRemove(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorktreeJsonContext.Default.WorktreeRemoveParams) is not { } p)
            return ctx.Fail("bad_params", "worktreeBayId is required");

        return await manager.RemoveWorktreeAsync(p.WorktreeBayId, p.Force).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("git_failed", "git worktree remove failed or not a worktree");
    }

    [CoveCommand("cove://commands/worktree.orphans")]
    public static async Task<ControlResponse> WorktreeOrphans(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorktreeJsonContext.Default.WorktreeListParams) is not { } p)
            return ctx.Fail("bad_params", "parentBayId is required");

        var orphans = await manager.WorktreeOrphansAsync(p.ParentBayId).ConfigureAwait(false);
        return ctx.Ok(new WorktreeOrphansResult(orphans), WorktreeJsonContext.Default.WorktreeOrphansResult);
    }

    [CoveCommand("cove://commands/worktree.prune")]
    public static async Task<ControlResponse> WorktreePrune(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorktreeJsonContext.Default.WorktreeListParams) is not { } p)
            return ctx.Fail("bad_params", "parentBayId is required");

        return await manager.PruneWorktreesAsync(p.ParentBayId).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("git_failed", "git worktree prune failed or parent not found");
    }

    [CoveCommand("cove://commands/worktree.adopt")]
    public static async Task<ControlResponse> WorktreeAdopt(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(WorktreeJsonContext.Default.WorktreeAdoptParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Location) || string.IsNullOrWhiteSpace(p.Branch))
            return ctx.Fail("bad_params", "parentBayId, branch and location are required");
        var bay = await manager.AdoptWorktreeAsync(p.ParentBayId, p.Location, p.Branch).ConfigureAwait(false);
        return bay is null
            ? ctx.Fail("not_orphan", "path is not an orphan worktree or parent not found")
            : ctx.Ok(new WorktreeIdResult(bay.Id), WorktreeJsonContext.Default.WorktreeIdResult);
    }
}

public sealed record WorktreeCreateParams(string ParentBayId, string Branch, string? Location = null, bool NewBranch = true, string? BaseRef = null);
public sealed record WorktreeListParams(string ParentBayId);
public sealed record WorktreeRemoveParams(string WorktreeBayId, bool Force = true);
public sealed record WorktreeAdoptParams(string ParentBayId, string Location, string Branch);
public sealed record WorktreeIdResult(string BayId);
public sealed record WorktreeSummary(string BayId, string Branch, string ProjectDir);
public sealed record WorktreeListResult(IReadOnlyList<WorktreeSummary> Worktrees);
public sealed record WorktreeOrphansResult(IReadOnlyList<string> Paths);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WorktreeCreateParams))]
[JsonSerializable(typeof(WorktreeListParams))]
[JsonSerializable(typeof(WorktreeRemoveParams))]
[JsonSerializable(typeof(WorktreeAdoptParams))]
[JsonSerializable(typeof(WorktreeIdResult))]
[JsonSerializable(typeof(WorktreeListResult))]
[JsonSerializable(typeof(WorktreeOrphansResult))]
public sealed partial class WorktreeJsonContext : JsonSerializerContext { }
