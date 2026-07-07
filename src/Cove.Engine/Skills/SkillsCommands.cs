using System.Linq;
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
}
