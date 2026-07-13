using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Launch;

public static class LauncherOptionsCommands
{
    [CoveCommand("cove://commands/launcher.options")]
    public static async Task<ControlResponse> Options(EngineDispatchContext ctx)
    {
        if (ctx.Launcher is not { } launcher)
            return ctx.Fail("not_ready", "launcher not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LauncherOptionsParams) is not { } p)
            return ctx.Fail("invalid_params", "launcher options params required");

        var result = await launcher.LoadLauncherOptionsAsync(p.Adapter);
        if (result is null)
            return ctx.Fail("not_found", $"no launcher_options for adapter {p.Adapter}");

        var dtos = result.Options.Select(o => new LauncherOptionDto(
            o.Key, o.Label, o.Type, o.DefaultValueRaw,
            o.Choices?.Select(c => new LauncherOptionChoiceDto(c.Value, c.Label)).ToArray())).ToArray();
        var flags = result.SuggestedFlags.Select(f => new LauncherSuggestedFlagDto(
            f.Flag, f.Description, f.Values?.ToArray() ?? null)).ToArray();
        return ctx.Ok(new LauncherOptionsResponse(dtos, flags), CoveJsonContext.Default.LauncherOptionsResponse);
    }
}
