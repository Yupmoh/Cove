using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Tasks;

public static class BindingCommands
{
    [CoveCommand("cove://commands/task.binding.get")]
    public static Task<ControlResponse> Get(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskBindingGetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "binding get params required"));
        var binding = svc.GetBinding(p.CardId);
        var info = new TaskBindingInfo(binding.AgentRef, binding.ProfileSlug, binding.Skills.Select(s => new SkillSelectionDto(s.Provenance, s.Name, s.Mode)).ToArray());
        return Task.FromResult(ctx.Ok(info, CoveJsonContext.Default.TaskBindingInfo));
    }

    [CoveCommand("cove://commands/task.binding.set")]
    public static async Task<ControlResponse> Set(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskBindingSetParams) is not { } p)
            return ctx.Fail("invalid_params", "binding set params required");
        var skills = (p.Skills ?? []).Select(s => new Cove.Tasks.SkillSelection(s.Provenance, s.Name, s.Mode)).ToList();
        await svc.SetBindingAsync(p.CardId, p.AgentRef, skills, p.ProfileSlug);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/task.binding.resolve-profile")]
    public static Task<ControlResponse> ResolveProfile(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskProfileResolveParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "profile resolve params required"));
        var payload = svc.ResolveTaskProfile(p.CardId);
        var dto = new TaskProfilePayloadDto(payload.AgentRef, payload.ProfileSlug, payload.Skills.Select(s => new SkillSelectionDto(s.Provenance, s.Name, s.Mode)).ToArray());
        return Task.FromResult(ctx.Ok(dto, CoveJsonContext.Default.TaskProfilePayloadDto));
    }
}
