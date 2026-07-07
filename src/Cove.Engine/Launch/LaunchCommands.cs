using System.Text.Json;
using Cove.Engine.Restart;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Launch;

public static class LaunchCommands
{
    [CoveCommand("cove://commands/launch.build")]
    public static Task<ControlResponse> Build(EngineDispatchContext ctx)
    {
        if (ctx.Launcher is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "launch orchestrator not available"));
        if (ctx.LaunchProfiles is not { } profiles)
            return Task.FromResult(ctx.Fail("not_ready", "launch profile store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LaunchBuildParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "launch build params required"));

        var profile = profiles.Load(p.Adapter, p.ProfileSlug);
        if (profile is null)
            return Task.FromResult(ctx.Fail("not_found", $"profile '{p.Adapter}/{p.ProfileSlug}' not found"));

        var overrides = ToOverrides(p.Yolo, p.WorkingDir, p.ExtraFlags, p.Env);
        var cmd = orch.BuildLaunchCommand(profile, overrides);
        return Task.FromResult(ctx.Ok(ToDto(cmd), CoveJsonContext.Default.ResumeCommandDto));
    }

    [CoveCommand("cove://commands/launch.overrides.save")]
    public static Task<ControlResponse> SaveOverrides(EngineDispatchContext ctx)
    {
        if (ctx.Launcher is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "launch orchestrator not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LaunchOverrideSaveParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "override save params required"));

        var overrides = ToOverrides(p.Yolo, p.WorkingDir, p.ExtraFlags, p.Env);
        orch.PersistOverrides(p.PaneId, overrides);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/launch.overrides.get")]
    public static Task<ControlResponse> GetOverrides(EngineDispatchContext ctx)
    {
        if (ctx.Launcher is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "launch orchestrator not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LaunchOverrideGetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "override get params required"));

        var overrides = orch.GetOverrides(p.PaneId);
        if (overrides is null)
            return Task.FromResult(ctx.Fail("not_found", $"no overrides for pane {p.PaneId}"));
        return Task.FromResult(ctx.Ok(ToDto(overrides), CoveJsonContext.Default.LauncherOverridesDto));
    }

    [CoveCommand("cove://commands/launch.overrides.clear")]
    public static Task<ControlResponse> ClearOverrides(EngineDispatchContext ctx)
    {
        if (ctx.Launcher is not { } orch)
            return Task.FromResult(ctx.Fail("not_ready", "launch orchestrator not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LaunchOverrideGetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "override get params required"));

        orch.ClearOverrides(p.PaneId);
        return Task.FromResult(ctx.Ok());
    }

    private static LauncherOverrides ToOverrides(bool yolo, string? workingDir, string[] extraFlags, Dictionary<string, string> env)
        => new() { Yolo = yolo, WorkingDir = workingDir, ExtraFlags = extraFlags ?? [], Env = env ?? new() };

    private static ResumeCommandDto ToDto(ResumeCommand cmd)
        => new(cmd.Command, cmd.Args.ToArray(), cmd.Cwd);

    private static LauncherOverridesDto ToDto(LauncherOverrides o)
        => new(o.Yolo, o.WorkingDir, o.ExtraFlags.ToArray(), o.Env.ToDictionary(e => e.Key, e => e.Value));
}
