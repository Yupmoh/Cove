using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Feedback;

public static class FeedbackCommands
{
    [CoveCommand("cove://commands/feedback.save")]
    public static Task<ControlResponse> Save(EngineDispatchContext context)
    {
        if (context.FeedbackStore is not { } store)
            return Task.FromResult(context.Fail("not_ready", "feedback store not available"));
        if (context.Request.Params is not JsonElement element
            || element.Deserialize(CoveJsonContext.Default.FeedbackSaveParams) is not { } parameters
            || string.IsNullOrWhiteSpace(parameters.Json))
            return Task.FromResult(context.Fail("invalid_params", "json required"));

        var result = store.Save(parameters.Json, parameters.Slug);
        return Task.FromResult(context.Ok(result, CoveJsonContext.Default.FeedbackSaveResult));
    }
}
