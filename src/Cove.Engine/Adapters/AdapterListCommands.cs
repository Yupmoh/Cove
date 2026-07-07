using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Adapters;

public static class AdapterListCommands
{
    [CoveCommand("cove://commands/adapter.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.ManifestStore is not { } manifestStore)
            return Task.FromResult(ctx.Fail("not_ready", "manifest store not available"));

        var items = new List<AdapterListItemDto>();
        foreach (var manifest in manifestStore.LoadAll())
            items.Add(new AdapterListItemDto(manifest.Name, manifest.DisplayName, manifest.Accent, manifest.Binary));

        return Task.FromResult(ctx.Ok(new AdapterListResult(items), CoveJsonContext.Default.AdapterListResult));
    }
}
