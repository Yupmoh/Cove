using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Cove.Adapters;
using Cove.Protocol;

namespace Cove.Engine;

internal static class SkillsPromptSigilCommands
{
    [CoveCommand("cove://commands/skills.resolve-prompt-sigils")]
    public static Task<ControlResponse> ResolvePromptSigils(EngineDispatchContext ctx)
    {
        if (ctx.Skills is not { } skills)
            return Task.FromResult(ctx.Fail("not_ready", "skills service unavailable"));

        string? prompt = null;
        if (ctx.Request.Params is JsonElement el && el.ValueKind == JsonValueKind.Object && el.TryGetProperty("prompt", out var promptEl))
            prompt = promptEl.GetString();

        if (string.IsNullOrEmpty(prompt))
            return Task.FromResult(ctx.Fail("invalid_params", "prompt required"));

        var resolver = new SigilResolver(skills.Index);
        var matches = resolver.Scan(prompt);
        var resolved = matches.Where(m => m.Skill is not null).Select(m => new ResolvedSigil(m.Name, m.Scope, m.Skill!.Body ?? "")).ToArray();
        var unresolved = matches.Where(m => m.Skill is null).Select(m => m.Name).ToArray();
        return Task.FromResult(ctx.Ok(new SigilResolutionResult(resolved, unresolved), CoveJsonContext.Default.SigilResolutionResult));
    }
}
