using System.Text.Json;
using Cove.Protocol;

namespace Cove.Engine.Keybindings;

public static class KeybindingCommands
{
    [CoveCommand("cove://commands/keybind.list")]
    public static Task<ControlResponse> KeybindList(EngineDispatchContext ctx)
    {
        if (ctx.Keybindings is not { } kb)
            return Task.FromResult(ctx.Fail("not_ready", "keybinding engine unavailable"));
        var bindings = kb.GetResolvedBindings().Select(b => new KeybindDto(b.Chord, b.ActionType, b.Action, b.Description)).ToList();
        var conflicts = kb.GetConflicts().ToList();
        return Task.FromResult(ctx.Ok(new KeybindListResult(bindings, conflicts), CoveJsonContext.Default.KeybindListResult));
    }

    [CoveCommand("cove://commands/keybind.set")]
    public static Task<ControlResponse> KeybindSet(EngineDispatchContext ctx)
    {
        if (ctx.Keybindings is not { } kb)
            return Task.FromResult(ctx.Fail("not_ready", "keybinding engine unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.KeybindSetParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "chord and action required"));
        var binding = new KeyBinding(p.Chord, p.ActionType, p.Action, p.Description);
        var result = kb.TrySetOverride(p.Chord, binding);
        if (!result.Success)
            return Task.FromResult(ctx.Fail("reserved", result.Error ?? "chord is reserved"));
        if (ctx.Config is not null)
        {
            try { ctx.Config.SetKeybindings(kb.ToJson()); } catch { }
        }
        var warningDto = result.Warning is not null ? new KeybindWarningDto(result.Warning) : null;
        return Task.FromResult(ctx.Ok(new KeybindSetResult(true, warningDto), CoveJsonContext.Default.KeybindSetResult));
    }

    [CoveCommand("cove://commands/keybind.clear")]
    public static Task<ControlResponse> KeybindClear(EngineDispatchContext ctx)
    {
        if (ctx.Keybindings is not { } kb)
            return Task.FromResult(ctx.Fail("not_ready", "keybinding engine unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.KeybindClearParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "chord required"));
        kb.ClearOverride(p.Chord);
        if (ctx.Config is not null) { try { ctx.Config.SetKeybindings(kb.ToJson()); } catch { } }
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/keybind.conflicts")]
    public static Task<ControlResponse> KeybindConflicts(EngineDispatchContext ctx)
    {
        if (ctx.Keybindings is not { } kb)
            return Task.FromResult(ctx.Fail("not_ready", "keybinding engine unavailable"));
        var conflicts = kb.GetConflicts().ToList();
        return Task.FromResult(ctx.Ok(new KeybindConflictsResult(conflicts), CoveJsonContext.Default.KeybindConflictsResult));
    }

    [CoveCommand("cove://commands/keybind.is-reserved")]
    public static Task<ControlResponse> KeybindIsReserved(EngineDispatchContext ctx)
    {
        if (ctx.Keybindings is not { } kb)
            return Task.FromResult(ctx.Fail("not_ready", "keybinding engine unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.KeybindChordParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "chord required"));
        return Task.FromResult(ctx.Ok(new KeybindReservedResult(kb.IsReserved(p.Chord)), CoveJsonContext.Default.KeybindReservedResult));
    }
}
