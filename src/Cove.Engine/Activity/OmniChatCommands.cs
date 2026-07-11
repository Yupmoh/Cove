using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Activity;

public static class OmniChatCommands
{
    [CoveCommand("cove://commands/omni-chat.append")]
    public static Task<ControlResponse> Append(EngineDispatchContext ctx)
    {
        if (ctx.OmniChat is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "omni chat store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.OmniChatAppendParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "omni chat append params required"));

        store.Append(p.NookId, new OmniChatMessage(p.Role, p.Body, DateTimeOffset.UtcNow));
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/omni-chat.history")]
    public static Task<ControlResponse> History(EngineDispatchContext ctx)
    {
        if (ctx.OmniChat is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "omni chat store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.OmniChatHistoryParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "omni chat history params required"));

        var messages = store.LoadHistory(p.NookId);
        var dtos = messages.Select(m => new OmniChatMessageDto(m.Role, m.Body, m.SentAt)).ToArray();
        return Task.FromResult(ctx.Ok(new OmniChatHistoryResult(dtos), CoveJsonContext.Default.OmniChatHistoryResult));
    }

    [CoveCommand("cove://commands/omni-chat.clear")]
    public static Task<ControlResponse> Clear(EngineDispatchContext ctx)
    {
        if (ctx.OmniChat is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "omni chat store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.OmniChatClearParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "omni chat clear params required"));

        store.Clear(p.NookId);
        return Task.FromResult(ctx.Ok());
    }
}
