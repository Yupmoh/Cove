using System.Linq;
using System.Text.Json;
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

    [CoveCommand("cove://commands/launch-profile.get")]
    public static Task<ControlResponse> Get(EngineDispatchContext ctx)
    {
        if (ctx.LaunchProfiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "launch profile store unavailable"));
        if (ctx.Request.Params is not System.Text.Json.JsonElement el || el.Deserialize(CoveJsonContext.Default.LaunchProfileGetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "adapter and slug required"));
        var profile = store.Load(p.Adapter, p.Slug);
        if (profile is null)
            return Task.FromResult(ctx.Fail("not_found", $"no launch profile {p.Slug} for adapter {p.Adapter}"));
        return Task.FromResult(ctx.Ok(ToDetail(profile), CoveJsonContext.Default.LaunchProfileDetail));
    }

    [CoveCommand("cove://commands/launch-profile.create")]
    public static Task<ControlResponse> Create(EngineDispatchContext ctx)
    {
        if (ctx.LaunchProfiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "launch profile store unavailable"));
        if (ctx.Request.Params is not System.Text.Json.JsonElement el || el.Deserialize(CoveJsonContext.Default.LaunchProfileCreateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "adapter, slug, and name required"));

        var errors = ValidateCreate(p);
        if (errors.Count > 0)
            return Task.FromResult(ctx.Fail("invalid_params", string.Join("; ", errors)));

        if (store.Load(p.Adapter, p.Slug) is not null)
            return Task.FromResult(ctx.Fail("conflict", $"profile {p.Slug} already exists for adapter {p.Adapter}"));

        var isFirst = store.List(p.Adapter).Count == 0;
        var profile = new LaunchProfile(
            p.Name,
            p.Slug,
            p.Adapter,
            p.IsDefault ?? isFirst,
            p.Model,
            p.Effort,
            p.CliArgs ?? System.Array.Empty<string>(),
            p.Env ?? new System.Collections.Generic.Dictionary<string, string>(),
            p.Permissions ?? new System.Collections.Generic.Dictionary<string, bool>(),
            p.Skills ?? System.Array.Empty<string>(),
            p.Agent,
            1);
        store.Save(profile);
        return Task.FromResult(ctx.Ok(ToDetail(profile), CoveJsonContext.Default.LaunchProfileDetail));
    }

    [CoveCommand("cove://commands/launch-profile.update")]
    public static Task<ControlResponse> Update(EngineDispatchContext ctx)
    {
        if (ctx.LaunchProfiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "launch profile store unavailable"));
        if (ctx.Request.Params is not System.Text.Json.JsonElement el || el.Deserialize(CoveJsonContext.Default.LaunchProfileUpdateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "adapter and slug required"));

        var existing = store.Load(p.Adapter, p.Slug);
        if (existing is null)
            return Task.FromResult(ctx.Fail("not_found", $"no launch profile {p.Slug} for adapter {p.Adapter}"));

        if (p.Name is not null && string.IsNullOrEmpty(p.Name))
            return Task.FromResult(ctx.Fail("invalid_params", "name must not be empty"));

        var merged = existing with
        {
            Name = p.Name ?? existing.Name,
            Model = p.Model ?? existing.Model,
            Effort = p.Effort ?? existing.Effort,
            CliArgs = p.CliArgs ?? existing.CliArgs,
            Env = p.Env ?? existing.Env,
            Permissions = p.Permissions ?? existing.Permissions,
            Skills = p.Skills ?? existing.Skills,
            Agent = p.Agent ?? existing.Agent,
        };
        if (p.IsDefault is bool makeDefault && makeDefault && !merged.IsDefault)
        {
            merged = merged with { IsDefault = true };
            store.SetDefault(p.Adapter, p.Slug);
        }
        store.Save(merged);
        return Task.FromResult(ctx.Ok(ToDetail(merged), CoveJsonContext.Default.LaunchProfileDetail));
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

    private static LaunchProfileDetail ToDetail(LaunchProfile p) => new(
        p.Slug, p.Name, p.Adapter, p.IsDefault, p.Model, p.Effort,
        p.CliArgs.ToArray(), p.Env, p.Permissions, p.Skills.ToArray(), p.Agent, p.SchemaVersion);

    private static System.Collections.Generic.List<string> ValidateCreate(LaunchProfileCreateParams p)
    {
        var errors = new System.Collections.Generic.List<string>();
        if (!LaunchProfileValidator.IsValidSlug(p.Slug))
            errors.Add("slug must be kebab-case, 1-64 chars [a-z0-9-]");
        if (!LaunchProfileValidator.IsValidAdapter(p.Adapter))
            errors.Add("adapter must be kebab-case, 1-64 chars [a-z0-9-]");
        if (string.IsNullOrEmpty(p.Name))
            errors.Add("name is required");
        return errors;
    }
}
