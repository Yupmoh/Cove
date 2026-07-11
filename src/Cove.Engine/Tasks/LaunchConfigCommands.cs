using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;
using Cove.Tasks.LaunchConfig;

namespace Cove.Engine.Tasks;

public static class LaunchConfigCommands
{
    [CoveCommand("cove://commands/task.launch-config.get")]
    public static Task<ControlResponse> Get(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LaunchConfigGetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "launch config get params required"));
        var config = svc.GetLaunchConfig(p.CardId);
        if (config is null)
            return Task.FromResult(ctx.OkJson("{}"));
        var info = ToInfo(config);
        return Task.FromResult(ctx.Ok(info, CoveJsonContext.Default.LaunchConfigInfo));
    }

    [CoveCommand("cove://commands/task.launch-config.set")]
    public static async Task<ControlResponse> Set(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LaunchConfigSetParams) is not { } p)
            return ctx.Fail("invalid_params", "launch config set params required");
        var config = new LaunchConfigModel
        {
            Adapter = p.Adapter,
            ProfileSlug = p.ProfileSlug,
            ExecutionMode = p.ExecutionMode ?? "nook",
            InProgressStatusId = p.InProgressStatusId,
            ReviewStatusId = p.ReviewStatusId,
            CompletionStatusId = p.CompletionStatusId,
            MergeTarget = p.MergeTarget,
            WorktreeBranchSource = p.WorktreeBranchSource,
            WorktreeBranchName = p.WorktreeBranchName,
        };
        var context = BuildValidationContext(ctx, p.CardId);
        var result = await svc.SetLaunchConfigAsync(p.CardId, config, context);
        if (!result.IsValid)
            return ctx.Ok(new LaunchConfigValidationResultDto(false, result.Errors.ToArray()), CoveJsonContext.Default.LaunchConfigValidationResultDto);
        return ctx.Ok(new LaunchConfigValidationResultDto(true, []), CoveJsonContext.Default.LaunchConfigValidationResultDto);
    }

    private static LaunchConfigValidationContext BuildValidationContext(EngineDispatchContext ctx, string cardId)
    {
        var card = ctx.TaskService?.GetCard(cardId);
        var bayId = card?.BayId ?? "";
        var statuses = ctx.TaskService?.ListStatuses(bayId, includeHidden: true).Select(s => s.Id).ToHashSet() ?? new HashSet<string>();
        var adapters = ctx.ManifestStore?.LoadAll().Select(m => m.Name).ToHashSet() ?? new HashSet<string>();
        var profiles = ctx.LaunchProfiles?.ListAll().Select(p => p.Slug).ToHashSet() ?? new HashSet<string>();
        return new LaunchConfigValidationContext(adapters, statuses, profiles);
    }

    private static LaunchConfigInfo ToInfo(LaunchConfigModel config) =>
        new(config.Adapter, config.ProfileSlug, config.ExecutionMode, config.InProgressStatusId, config.ReviewStatusId, config.CompletionStatusId, config.MergeTarget, config.WorktreeBranchSource, config.WorktreeBranchName);
}
