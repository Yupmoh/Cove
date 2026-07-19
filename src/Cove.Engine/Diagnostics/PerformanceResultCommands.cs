using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Diagnostics;

public static class PerformanceResultCommands
{
    [CoveCommand("cove://commands/perf.result.save")]
    public static Task<ControlResponse> Save(EngineDispatchContext context)
    {
        if (context.PerformanceResults is not { } store)
            return Task.FromResult(context.Fail("not_ready", "performance result store not available"));
        if (context.Request.Params is not JsonElement element
            || element.Deserialize(CoveJsonContext.Default.PerformanceResultSaveParams) is not { } parameters)
            return Task.FromResult(context.Fail("invalid_params", "json and markdown required"));

        var result = store.Save(parameters.Json, parameters.Markdown);
        return Task.FromResult(context.Ok(result, CoveJsonContext.Default.PerformanceResultSaveResult));
    }
}
