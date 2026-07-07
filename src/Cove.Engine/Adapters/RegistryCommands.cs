using Cove.Adapters;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Adapters;

public static class RegistryCommands
{
    [CoveCommand("cove://commands/registry.fetch")]
    public static async Task<ControlResponse> Fetch(EngineDispatchContext ctx)
    {
        if (ctx.Registry is not { } registry)
            return ctx.Fail("not_ready", "registry service not available");

        var reg = await registry.FetchAsync().ConfigureAwait(false);
        if (reg is null)
            return ctx.Fail("unavailable", "registry could not be fetched and no cache is available");

        var entries = reg.Adapters.Select(a => new RegistryEntryDto(a.Name, a.DisplayName, a.Version, a.Official)).ToArray();
        return ctx.Ok(new RegistryFetchResult(entries), CoveJsonContext.Default.RegistryFetchResult);
    }
}
