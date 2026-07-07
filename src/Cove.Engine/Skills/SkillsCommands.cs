using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Cove.Protocol;

namespace Cove.Engine;

internal static class SkillsCommands
{
    [CoveCommand("cove://commands/skills.index")]
    public static Task<ControlResponse> SkillsIndex(EngineDispatchContext ctx)
    {
        if (ctx.Skills is not { } skills)
            return Task.FromResult(ctx.Fail("not_ready", "skills service unavailable"));
        var entries = skills.List().Select(s => new SkillIndexItem(
            s.Name, s.Description, s.Source.ToString(), s.Provenance, s.AdapterName)).ToArray();
        return Task.FromResult(ctx.Ok(new SkillsIndexResult(entries), CoveJsonContext.Default.SkillsIndexResult));
    }

    [CoveCommand("cove://commands/skills.resolve-manifest")]
    public static Task<ControlResponse> ResolveManifest(EngineDispatchContext ctx)
    {
        if (ctx.Skills is not { } skills)
            return Task.FromResult(ctx.Fail("not_ready", "skills service unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.SkillsResolveManifestParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "name required"));

        var skill = skills.Resolve(p.Name);
        if (skill is null)
            return Task.FromResult(ctx.Fail("not_found", $"skill '{p.Name}' not found — resolve-manifest requires the exact skill name; prefixes are rejected"));

        return Task.FromResult(ctx.Ok(new SkillManifestResult(skill.Name, skill.Body ?? "", skill.Source.ToString()), CoveJsonContext.Default.SkillManifestResult));
    }
}
