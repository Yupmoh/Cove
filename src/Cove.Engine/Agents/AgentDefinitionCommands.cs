using System.Linq;
using System.Threading.Tasks;
using Cove.Adapters;
using Cove.Protocol;

namespace Cove.Engine;

internal static class AgentDefinitionCommands
{
    [CoveCommand("cove://commands/agent.definition.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.Agents is not { } agents)
            return Task.FromResult(ctx.Fail("not_ready", "agent store unavailable"));
        var items = agents.List().Select(a => new AgentDefinitionListItem(a.Slug, a.Name, a.Description, a.Adapter, a.AttachedSkills.Count)).ToArray();
        return Task.FromResult(ctx.Ok(new AgentDefinitionListResult(items), CoveJsonContext.Default.AgentDefinitionListResult));
    }

    [CoveCommand("cove://commands/agent.definition.show")]
    public static Task<ControlResponse> Show(EngineDispatchContext ctx)
    {
        if (ctx.Agents is not { } agents)
            return Task.FromResult(ctx.Fail("not_ready", "agent store unavailable"));
        if (ctx.Request.Params is not System.Text.Json.JsonElement el || el.TryGetProperty("slug", out var slugEl) is false)
            return Task.FromResult(ctx.Fail("invalid_params", "slug required"));
        var slug = slugEl.GetString();
        if (slug is null || !AgentDefinitionValidator.IsValidSlug(slug))
            return Task.FromResult(ctx.Fail("invalid_params", "invalid slug"));
        var agent = agents.Load(slug);
        if (agent is null)
            return Task.FromResult(ctx.Fail("not_found", $"agent {slug} not found"));
        return Task.FromResult(ctx.Ok(new AgentDefinitionShowResult(agent.Slug, agent.Name, agent.Description, agent.Adapter, agent.Prompt, agent.AttachedSkills.ToArray()), CoveJsonContext.Default.AgentDefinitionShowResult));
    }

    [CoveCommand("cove://commands/agent.definition.delete")]
    public static Task<ControlResponse> Delete(EngineDispatchContext ctx)
    {
        if (ctx.Agents is not { } agents)
            return Task.FromResult(ctx.Fail("not_ready", "agent store unavailable"));
        if (ctx.Request.Params is not System.Text.Json.JsonElement el || el.TryGetProperty("slug", out var slugEl) is false)
            return Task.FromResult(ctx.Fail("invalid_params", "slug required"));
        var slug = slugEl.GetString();
        if (slug is null || !AgentDefinitionValidator.IsValidSlug(slug))
            return Task.FromResult(ctx.Fail("invalid_params", "invalid slug"));
        agents.Delete(slug);
        return Task.FromResult(ctx.Ok());
    }
}
