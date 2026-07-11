using System.Text.Json;
using Cove.Protocol;

namespace Cove.Engine;

internal static class EngineCommands
{
    [CoveCommand("cove://commands/nook.list")]
    public static Task<ControlResponse> NookList(EngineDispatchContext ctx)
    {
        NookInfo[] nooks = ctx.Nooks is { } reg ? reg.List() : Array.Empty<NookInfo>();
        return Task.FromResult(ctx.Ok(new NookListResult(nooks), CoveJsonContext.Default.NookListResult));
    }

    [CoveCommand("cove://commands/nook.spawn")]
    public static Task<ControlResponse> NookSpawn(EngineDispatchContext ctx)
    {
        if (ctx.Nooks is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "nook registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.SpawnParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "spawn params required"));
        string? bayDir = null;
        if (ctx.Bays is { } wm
            && wm.Registry.FocusedBayId is { } focusedId
            && wm.Get(focusedId) is { } focusedActor
            && !string.IsNullOrEmpty(focusedActor.State.ProjectDir))
            bayDir = focusedActor.State.ProjectDir;
        NookInfo info = reg.Spawn(p, bayDir);
        if (p.Adapter is { } adapter)
        {
            ctx.AgentRouter?.Register(info.NookId, adapter, p.AgentName, p.Bay, p.Shore, mcpAccessScope: p.McpAccessScope, mcpVisible: p.McpVisible);
            ctx.Sessions?.Register(info.NookId, adapter, p.SessionId);
            ctx.Launcher?.PersistOverrides(info.NookId, new Cove.Engine.Restart.LauncherOverrides { Yolo = p.Yolo });
            ctx.HookRouter?.Seed(info.NookId, adapter);
            ctx.RecentSessions?.RecordStart(adapter, info.NookId, p.Bay ?? "", p.Cwd ?? bayDir ?? "", System.DateTimeOffset.UtcNow);
        }
        return Task.FromResult(ctx.Ok(info, CoveJsonContext.Default.NookInfo));
    }

    [CoveCommand("cove://commands/nook.write")]
    public static Task<ControlResponse> NookWrite(EngineDispatchContext ctx)
    {
        if (ctx.Nooks is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "nook registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookWriteParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "write params required"));
        var resolved = reg.ResolveId(p.NookId);
        if (!resolved.Found)
            return Task.FromResult(ctx.Fail(resolved.ErrorCode ?? "not_found", $"unknown nook {p.NookId}"));
        byte[] data = Convert.FromBase64String(p.DataBase64);
        return Task.FromResult(reg.Write(resolved.Id!, data)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"unknown nook {p.NookId}"));
    }

    [CoveCommand("cove://commands/nook.resize")]
    public static Task<ControlResponse> NookResize(EngineDispatchContext ctx)
    {
        if (ctx.Nooks is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "nook registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ResizeParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "resize params required"));
        var resolved = reg.ResolveId(p.NookId);
        if (!resolved.Found)
            return Task.FromResult(ctx.Fail(resolved.ErrorCode ?? "not_found", $"unknown nook {p.NookId}"));
        return Task.FromResult(reg.Resize(resolved.Id!, p.Cols, p.Rows)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"unknown nook {p.NookId}"));
    }

    [CoveCommand("cove://commands/nook.kill")]
    public static Task<ControlResponse> NookKill(EngineDispatchContext ctx)
    {
        if (ctx.Nooks is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "nook registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookRefParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "nook ref required"));
        var resolved = reg.ResolveId(p.NookId);
        if (!resolved.Found)
            return Task.FromResult(ctx.Fail(resolved.ErrorCode ?? "not_found", $"unknown nook {p.NookId}"));
        ctx.AgentRouter?.Unregister(resolved.Id!);
        return Task.FromResult(reg.Kill(resolved.Id!)
                ? ctx.Ok()
                : ctx.Fail("not_found", $"unknown nook {p.NookId}"));
    }

    [CoveCommand("cove://commands/nook.search")]
    public static Task<ControlResponse> NookSearch(EngineDispatchContext ctx)
    {
        if (ctx.Nooks is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "nook registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.SearchParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "search params required"));
        var resolved = reg.ResolveId(p.NookId);
        var searchNookId = resolved.Found ? resolved.Id! : p.NookId;
        var matches = reg.Search(searchNookId, p.Query, p.CaseSensitive);
        return Task.FromResult(ctx.Ok(new SearchResult(matches), CoveJsonContext.Default.SearchResult));
    }

    [CoveCommand("cove://commands/nook.rename")]
    public static Task<ControlResponse> NookRename(EngineDispatchContext ctx)
    {
        if (ctx.Nooks is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "nook registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookRenameParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "rename params required"));
        var resolved = reg.ResolveId(p.NookId);
        if (!resolved.Found)
            return Task.FromResult(ctx.Fail(resolved.ErrorCode ?? "not_found", $"unknown nook {p.NookId}"));
        return Task.FromResult(reg.Rename(resolved.Id!, p.Title)
            ? ctx.Ok()
            : ctx.Fail("not_found", $"unknown nook {p.NookId}"));
    }

    [CoveCommand("cove://commands/nook.read")]
    public static Task<ControlResponse> NookRead(EngineDispatchContext ctx)
    {
        if (ctx.Nooks is not { } reg)
            return Task.FromResult(ctx.Fail("not_ready", "nook registry unavailable"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NookReadParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "read params required"));
        var resolved = reg.ResolveId(p.NookId);
        var readNookId = resolved.Found ? resolved.Id! : p.NookId;
        byte[] bytes = reg.Read(readNookId, p.Offset, p.MaxBytes);
        long head = 0;
        if (reg.TryGet(readNookId, out var nook))
            head = nook.Ring.Head;
        long nextOffset = p.Offset + bytes.Length;
        return Task.FromResult(ctx.Ok(new NookReadResult(System.Convert.ToBase64String(bytes), nextOffset, head), CoveJsonContext.Default.NookReadResult));
    }
}
