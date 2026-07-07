using System.Linq;
using System.Threading.Tasks;
using Cove.Adapters;
using Cove.Protocol;

namespace Cove.Engine;

internal static class LaunchProfileCommands
{
    [CoveCommand("cove://commands/launch-profile.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.LaunchProfiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "launch profile store unavailable"));
        var adapter = ctx.Request.Params is System.Text.Json.JsonElement el && el.TryGetProperty("adapter", out var a) ? a.GetString() : null;
        var profiles = adapter is null ? store.ListAll() : store.List(adapter);
        var items = profiles.Select(p => new LaunchProfileListItem(p.Slug, p.Name, p.Adapter, p.IsDefault, p.Model, p.Effort, p.CliArgs.Count, p.Env.Count)).ToArray();
        return Task.FromResult(ctx.Ok(new LaunchProfileListResult(items), CoveJsonContext.Default.LaunchProfileListResult));
    }

    [CoveCommand("cove://commands/launch-profile.set-default")]
    public static Task<ControlResponse> SetDefault(EngineDispatchContext ctx)
    {
        if (ctx.LaunchProfiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "launch profile store unavailable"));
        if (ctx.Request.Params is not System.Text.Json.JsonElement el)
            return Task.FromResult(ctx.Fail("invalid_params", "params required"));
        if (!el.TryGetProperty("adapter", out var adapterEl) || !el.TryGetProperty("slug", out var slugEl))
            return Task.FromResult(ctx.Fail("invalid_params", "adapter and slug required"));
        var adapter = adapterEl.GetString();
        var slug = slugEl.GetString();
        if (adapter is null || slug is null)
            return Task.FromResult(ctx.Fail("invalid_params", "adapter and slug required"));
        store.SetDefault(adapter, slug);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/launch-profile.delete")]
    public static Task<ControlResponse> Delete(EngineDispatchContext ctx)
    {
        if (ctx.LaunchProfiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "launch profile store unavailable"));
        if (ctx.Request.Params is not System.Text.Json.JsonElement el)
            return Task.FromResult(ctx.Fail("invalid_params", "params required"));
        if (!el.TryGetProperty("adapter", out var adapterEl) || !el.TryGetProperty("slug", out var slugEl))
            return Task.FromResult(ctx.Fail("invalid_params", "adapter and slug required"));
        var adapter = adapterEl.GetString();
        var slug = slugEl.GetString();
        if (adapter is null || slug is null)
            return Task.FromResult(ctx.Fail("invalid_params", "adapter and slug required"));
        store.Delete(adapter, slug);
        return Task.FromResult(ctx.Ok());
    }
}
