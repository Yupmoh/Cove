using System.IO;
using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Adapters;

public static class AdapterListCommands
{
    [CoveCommand("cove://commands/adapter.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.HookServer is not { } server)
            return Task.FromResult(ctx.Fail("not_ready", "hook server not available"));

        var adaptersRoot = Path.Combine(server.DataDir, "adapters");
        var items = new List<AdapterListItemDto>();
        if (Directory.Exists(adaptersRoot))
        {
            foreach (var dir in Directory.EnumerateDirectories(adaptersRoot))
            {
                var manifestPath = Path.Combine(dir, "adapter.json");
                if (!File.Exists(manifestPath))
                    continue;
                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var manifest = JsonSerializer.Deserialize(json, Cove.Adapters.AdaptersJsonContext.Default.AdapterManifest);
                    if (manifest is not null)
                        items.Add(new AdapterListItemDto(manifest.Name, manifest.DisplayName, manifest.Accent, manifest.Binary));
                }
                catch { }
            }
        }
        return Task.FromResult(ctx.Ok(new AdapterListResult(items), CoveJsonContext.Default.AdapterListResult));
    }
}
