using System.Text.Json;
using Cove.Engine.Restart;
using Cove.Protocol;

namespace Cove.Engine.Launch;

public static class LaunchCommands
{
    [CoveCommand("cove://commands/launch.build")]
    public static async Task<ControlResponse> Build(EngineDispatchContext ctx)
    {
        if (ctx.Launcher is not { } orch)
            return ctx.Fail("not_ready", "launch orchestrator not available");
        if (!orch.CanResolveProfiles)
            return ctx.Fail("not_ready", "launch profile store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LaunchBuildParams) is not { } p)
            return ctx.Fail("invalid_params", "launch build params required");

        var profile = orch.FindProfile(p.Adapter, p.ProfileSlug);
        if (profile is null)
            return ctx.Fail("not_found", $"profile '{p.Adapter}/{p.ProfileSlug}' not found");

        var overrides = ToOverrides(p.Yolo, p.WorkingDir, p.ExtraFlags, p.Env, p.Model, p.Effort);
        try
        {
            var cmd = await orch.BuildLaunchCommandAsync(profile, overrides).ConfigureAwait(false);
            return ctx.Ok(ToDto(cmd), CoveJsonContext.Default.ResumeCommandDto);
        }
        catch (Cove.Engine.Restart.ResumeFailedException ex)
        {
            return ctx.Fail("launch_failed", ex.Message);
        }
    }

    [CoveCommand("cove://commands/launch.overrides.save")]
    public static Task<ControlResponse> SaveOverrides(EngineDispatchContext ctx)
    {
        if (ctx.Launcher is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "launch orchestrator not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LaunchOverrideSaveParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "override save params required"));

        var overrides = ToOverrides(p.Yolo, p.WorkingDir, p.ExtraFlags, p.Env, p.Model, p.Effort);
        orch.PersistOverrides(p.NookId, overrides);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/launch.overrides.get")]
    public static Task<ControlResponse> GetOverrides(EngineDispatchContext ctx)
    {
        if (ctx.Launcher is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "launch orchestrator not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LaunchOverrideGetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "override get params required"));

        var overrides = orch.GetOverrides(p.NookId);
        if (overrides is null)
            return Task.FromResult(ctx.Fail("not_found", $"no overrides for nook {p.NookId}"));
        return Task.FromResult(ctx.Ok(ToDto(overrides), CoveJsonContext.Default.LauncherOverridesDto));
    }

    [CoveCommand("cove://commands/launch.overrides.clear")]
    public static Task<ControlResponse> ClearOverrides(EngineDispatchContext ctx)
    {
        if (ctx.Launcher is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "launch orchestrator not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LaunchOverrideGetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "override get params required"));

        orch.ClearOverrides(p.NookId);
        return Task.FromResult(ctx.Ok());
    }

    private static LauncherOverrides ToOverrides(
        bool yolo,
        string? workingDir,
        string[] extraFlags,
        Dictionary<string, string> env,
        string? model,
        string? effort) => new()
        {
            Yolo = yolo,
            WorkingDir = workingDir,
            ExtraFlags = extraFlags ?? [],
            Env = env ?? new(),
            Model = model,
            Effort = effort,
        };

    private static ResumeCommandDto ToDto(ResumeCommand cmd)
        => new(cmd.Command, cmd.Args.ToArray(), cmd.Cwd);

    private static LauncherOverridesDto ToDto(LauncherOverrides o)
        => new(o.Yolo, o.WorkingDir, o.ExtraFlags.ToArray(), o.Env.ToDictionary(e => e.Key, e => e.Value), o.Model, o.Effort);
}
