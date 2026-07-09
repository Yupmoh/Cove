using System.Text.Json;
using Cove.Protocol;

namespace Cove.Engine.Theming;

public static class ThemeCommands
{
    [CoveCommand("cove://commands/theme.list")]
    public static Task<ControlResponse> ThemeList(EngineDispatchContext ctx)
    {
        if (ctx.Themes is not { } themes)
            return Task.FromResult(ctx.Fail("not_ready", "theme service unavailable"));
        var dtos = themes.ListAll().Select(t => new ThemeDto(t.Name, t.Type, t.TerminalBackground, t.TerminalForeground, t.ChromeSurface, t.ChromeText, t.ChromeAccent)).ToList();
        return Task.FromResult(ctx.Ok(new ThemeListResult(dtos), CoveJsonContext.Default.ThemeListResult));
    }

    [CoveCommand("cove://commands/theme.get-active")]
    public static Task<ControlResponse> ThemeGetActive(EngineDispatchContext ctx)
    {
        if (ctx.Themes is not { } themes)
            return Task.FromResult(ctx.Fail("not_ready", "theme service unavailable"));
        var active = themes.GetActive();
        if (active is null)
            return Task.FromResult(ctx.Ok(new ThemeActiveResult(null), CoveJsonContext.Default.ThemeActiveResult));
        var dto = new ThemeDto(active.Name, active.Type, active.TerminalBackground, active.TerminalForeground, active.ChromeSurface, active.ChromeText, active.ChromeAccent);
        return Task.FromResult(ctx.Ok(new ThemeActiveResult(dto), CoveJsonContext.Default.ThemeActiveResult));
    }

    [CoveCommand("cove://commands/theme.set-active")]
    public static Task<ControlResponse> ThemeSetActive(EngineDispatchContext ctx)
    {
        if (ctx.Themes is not { } themes)
            return Task.FromResult(ctx.Fail("not_ready", "theme service unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ThemeRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "theme name required"));
        var theme = themes.SetActive(p.Name);
        var dto = new ThemeDto(theme.Name, theme.Type, theme.TerminalBackground, theme.TerminalForeground, theme.ChromeSurface, theme.ChromeText, theme.ChromeAccent);
        return Task.FromResult(ctx.Ok(new ThemeActiveResult(dto), CoveJsonContext.Default.ThemeActiveResult));
    }

    [CoveCommand("cove://commands/theme.save-custom")]
    public static Task<ControlResponse> ThemeSaveCustom(EngineDispatchContext ctx)
    {
        if (ctx.Themes is not { } themes)
            return Task.FromResult(ctx.Fail("not_ready", "theme service unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ThemeSaveParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "theme save params required"));
        var theme = new Theme(p.Name, p.Type, p.TerminalBackground, p.TerminalForeground, p.ChromeSurface, p.ChromeText, p.ChromeAccent);
        themes.SaveCustom(theme);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/theme.delete-custom")]
    public static Task<ControlResponse> ThemeDeleteCustom(EngineDispatchContext ctx)
    {
        if (ctx.Themes is not { } themes)
            return Task.FromResult(ctx.Fail("not_ready", "theme service unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ThemeRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "theme name required"));
        var deleted = themes.DeleteCustom(p.Name);
        if (!deleted)
            return Task.FromResult(ctx.Fail("not_found", $"custom theme '{p.Name}' not found or is builtin"));
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/theme.is-builtin")]
    public static Task<ControlResponse> ThemeIsBuiltin(EngineDispatchContext ctx)
    {
        if (ctx.Themes is not { } themes)
            return Task.FromResult(ctx.Fail("not_ready", "theme service unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ThemeRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "theme name required"));
        return Task.FromResult(ctx.Ok(new ThemeBuiltinResult(themes.IsBuiltin(p.Name)), CoveJsonContext.Default.ThemeBuiltinResult));
    }
}
