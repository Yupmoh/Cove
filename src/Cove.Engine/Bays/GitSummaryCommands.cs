using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Bays;

public static class GitSummaryCommands
{
    [CoveCommand("cove://commands/git.summary")]
    public static async Task<ControlResponse> Get(EngineDispatchContext context)
    {
        if (context.GitSummary is not { } service)
            return context.Fail("not_ready", "git summary service not available");
        if (context.Request.Params is not JsonElement element
            || element.Deserialize(CoveJsonContext.Default.GitSummaryParams) is not { } parameters
            || string.IsNullOrWhiteSpace(parameters.Path))
            return context.Fail("invalid_params", "path required");

        var result = await service.GetAsync(parameters.Path, context.CancellationToken).ConfigureAwait(false);
        return context.Ok(result, CoveJsonContext.Default.GitSummaryResult);
    }
}
