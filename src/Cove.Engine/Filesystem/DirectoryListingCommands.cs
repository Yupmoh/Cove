using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Filesystem;

public static class DirectoryListingCommands
{
    [CoveCommand("cove://commands/fs.list")]
    public static Task<ControlResponse> List(EngineDispatchContext context)
    {
        if (context.DirectoryListing is not { } service)
            return Task.FromResult(context.Fail("not_ready", "directory listing service not available"));
        if (context.Request.Params is not JsonElement element
            || element.Deserialize(CoveJsonContext.Default.DirectoryListParams) is not { } parameters
            || string.IsNullOrWhiteSpace(parameters.Path))
            return Task.FromResult(context.Fail("invalid_params", "path required"));

        var result = service.List(parameters.Path, parameters.Cap ?? 400);
        return Task.FromResult(context.Ok(result, CoveJsonContext.Default.DirectoryListResult));
    }
}
