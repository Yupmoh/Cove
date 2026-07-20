using System.Text.Json;
using Cove.Protocol;

namespace Cove.Engine.Agents;

public static class AgentMessageCommands
{
    [CoveCommand("cove://commands/agent.message")]
    public static async Task<ControlResponse> Message(EngineDispatchContext ctx)
    {
        if (ctx.AgentRouter is not { } router)
            return ctx.Fail("not_ready", "agent router not available");
        if (ctx.Nooks is not { } nooks)
            return ctx.Fail("not_ready", "nook registry not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.AgentMessageParams) is not { } p)
            return ctx.Fail("invalid_params", "missing or invalid params");

        var target = router.ResolveTarget(p.Target);
        if (target is null)
            return ctx.Fail("not_found", $"no agent matches '{p.Target}'");

        var senderNookId =
            ctx.Request.CallerNookId ?? p.FromNookId;
        var senderAgent = string.IsNullOrEmpty(senderNookId)
            ? null
            : router.ResolveTarget(senderNookId);
        var sender = new AgentMessageSender(
            senderNookId ?? "",
            p.FromAdapter ?? senderAgent?.Adapter ?? "",
            p.FromName ?? senderAgent?.Name);
        var body = p.NoFrame
            ? AgentMessageFramer.NoFrame(p.Body)
            : AgentMessageFramer.Frame(
                sender,
                p.Body,
                replyPrefix: senderNookId);

        var delivery = new AgentMessageDelivery(nooks);
        var delivered = await delivery.DeliverAsync(target.NookId, body, p.SubmitPauseMs).ConfigureAwait(false);
        if (!delivered)
            return ctx.Fail("write_failed", $"failed to write to nook {target.NookId}");

        return ctx.Ok();
    }

    [CoveCommand("cove://commands/agent.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.AgentRouter is not { } router)
            return Task.FromResult(ctx.Fail("not_ready", "agent router not available"));

        var parameters = ctx.Request.Params is JsonElement element
            ? element.Deserialize(
                CoveJsonContext.Default.AgentListParams)
            : null;
        var scope = parameters?.Scope ?? "same-tab";
        var requesterNookId = ctx.Request.CallerNookId;
        var requester = string.IsNullOrEmpty(requesterNookId)
            ? null
            : router.ResolveTarget(requesterNookId);
        var requesterBay = requester?.Bay;
        var requesterShore = requester?.Shore;

        var agents = router.List(scope, requesterBay, requesterNookId, requesterShore).ToList();
        var dtos = agents.Select(a => new AgentListDto(a.NookId, a.Adapter, a.Name, a.Bay, a.Shore, a.Status, a.McpAccessScope)).ToList();
        return Task.FromResult(ctx.Ok(new AgentListResult(dtos), CoveJsonContext.Default.AgentListResult));
    }
}
