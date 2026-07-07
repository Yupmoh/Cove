using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Agents;

public static class AgentMessageCommands
{
    [CoveCommand("cove://commands/agent.message")]
    public static async Task<ControlResponse> Message(EngineDispatchContext ctx)
    {
        if (ctx.AgentRouter is not { } router)
            return ctx.Fail("not_ready", "agent router not available");
        if (ctx.Panes is not { } panes)
            return ctx.Fail("not_ready", "pane registry not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.AgentMessageParams) is not { } p)
            return ctx.Fail("invalid_params", "missing or invalid params");

        var target = router.ResolveTarget(p.Target);
        if (target is null)
            return ctx.Fail("not_found", $"no agent matches '{p.Target}'");

        var sender = new AgentMessageSender(p.FromPaneId ?? "", p.FromAdapter ?? "", p.FromName);
        var body = p.NoFrame ? AgentMessageFramer.NoFrame(p.Body) : AgentMessageFramer.Frame(sender, p.Body, replyPrefix: p.FromPaneId);

        var delivery = new AgentMessageDelivery(panes);
        var delivered = await delivery.DeliverAsync(target.PaneId, body, p.SubmitPauseMs).ConfigureAwait(false);
        if (!delivered)
            return ctx.Fail("write_failed", $"failed to write to pane {target.PaneId}");

        return ctx.Ok();
    }

    [CoveCommand("cove://commands/agent.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.AgentRouter is not { } router)
            return Task.FromResult(ctx.Fail("not_ready", "agent router not available"));

        var scope = "same-tab";
        string? requesterPaneId = null;
        string? requesterWorkspace = null;
        string? requesterRoom = null;

        if (ctx.Request.Params is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("scope", out var s) && s.ValueKind == JsonValueKind.String)
                scope = s.GetString() ?? "same-tab";
            if (el.TryGetProperty("requesterPaneId", out var rp) && rp.ValueKind == JsonValueKind.String)
                requesterPaneId = rp.GetString();
            if (el.TryGetProperty("requesterWorkspace", out var rw) && rw.ValueKind == JsonValueKind.String)
                requesterWorkspace = rw.GetString();
            if (el.TryGetProperty("requesterRoom", out var rr) && rr.ValueKind == JsonValueKind.String)
                requesterRoom = rr.GetString();
        }

        var agents = router.List(scope, requesterWorkspace, requesterPaneId, requesterRoom).ToList();
        var dtos = agents.Select(a => new AgentListDto(a.PaneId, a.Adapter, a.Name, a.Workspace, a.Room, a.Status, a.McpAccessScope)).ToList();
        return Task.FromResult(ctx.Ok(new AgentListResult(dtos), CoveJsonContext.Default.AgentListResult));
    }
}
