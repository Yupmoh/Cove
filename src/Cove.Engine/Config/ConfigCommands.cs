using System.Text.Json;
using System.Threading.Tasks;
using Cove.Protocol;

namespace Cove.Engine.Config;

internal static class ConfigCommands
{
    [CoveCommand("cove://commands/config.get")]
    public static Task<ControlResponse> ConfigGet(EngineDispatchContext ctx)
    {
        if (ctx.Config is not { } config)
            return Task.FromResult(ctx.Fail("not_ready", "config service unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ConfigGetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "config get params required"));
        var value = config.Get(p.Key);
        return Task.FromResult(value is { } v
            ? ctx.Ok(new ConfigGetResult(v), CoveJsonContext.Default.ConfigGetResult)
            : ctx.Fail("not_found", $"config key '{p.Key}' not found"));
    }

    [CoveCommand("cove://commands/config.set")]
    public static Task<ControlResponse> ConfigSet(EngineDispatchContext ctx)
    {
        if (ctx.Config is not { } config)
            return Task.FromResult(ctx.Fail("not_ready", "config service unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ConfigSetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "config set params required"));
        config.Set(p.Key, p.Value);
        return Task.FromResult(ctx.Ok());
    }
}
