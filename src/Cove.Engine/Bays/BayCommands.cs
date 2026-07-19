using System.Text.Json;
using Cove.Protocol;

namespace Cove.Engine.Bays;

public static class BayCommands
{
    [CoveCommand("cove://commands/bay.create")]
    public static async Task<ControlResponse> BayCreate(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(BaysJsonContext.Default.BayCreateParams) is not { } p)
            return ctx.Fail("bad_params", "name is required");

        var outcome = await manager.CreateValidatedBayAsync(p.Name, p.ProjectDir, p.CollectionId).ConfigureAwait(false);
        if (outcome.Bay is not { } bay)
            return ctx.Fail(outcome.ErrorCode ?? "bad_params", outcome.ErrorMessage ?? "invalid params");
        return ctx.Ok(new BayIdResult(bay.Id), BaysJsonContext.Default.BayIdResult);
    }

    [CoveCommand("cove://commands/bay.switch")]
    public static async Task<ControlResponse> BaySwitch(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(BaysJsonContext.Default.BayIdParams) is not { } p)
            return ctx.Fail("bad_params", "id is required");

        if (!await manager.SwitchBayAsync(p.Id).ConfigureAwait(false))
            return ctx.Fail("not_found", $"bay {p.Id} not found");
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/bay.list")]
    public static Task<ControlResponse> BayList(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return Task.FromResult(ctx.Fail("no_bays", "bay manager unavailable"));
        return Task.FromResult(ctx.Ok(new BayListResult(manager.ListBays()), BaysJsonContext.Default.BayListResult));
    }

    [CoveCommand("cove://commands/bay.delete")]
    public static async Task<ControlResponse> BayDelete(EngineDispatchContext ctx)
    {
        if (ctx.Bays is not { } manager)
            return ctx.Fail("no_bays", "bay manager unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(BaysJsonContext.Default.BayIdParams) is not { } p)
            return ctx.Fail("bad_params", "id is required");

        if (manager.Get(p.Id) is null)
            return ctx.Fail("not_found", $"bay {p.Id} not found");
        var nookIds = manager.Layout.LeafNookIds(p.Id);
        if (!await manager.DeleteBayAsync(p.Id).ConfigureAwait(false))
            return ctx.Fail("not_found", $"bay {p.Id} not found");
        if (ctx.Nooks is { } nooks)
            foreach (var nookId in nookIds)
                nooks.Kill(nookId);
        return ctx.Ok();
    }
}
