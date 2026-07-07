using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Cove.Adapters;
using Cove.Protocol;

namespace Cove.Engine;

internal static class AdapterEnvCommands
{
    private const string MaskSentinel = "****";

    [CoveCommand("cove://commands/adapter-env.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.AdapterEnv is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "adapter env store unavailable"));
        var adapter = ExtractAdapter(ctx);
        if (adapter is null)
            return Task.FromResult(ctx.Fail("invalid_params", "valid adapter required"));
        var entries = store.Load(adapter);
        var items = entries.Select(e => new AdapterEnvVarItem(e.Key, EnvVarParser.MaskSecret(e.Key, e.Value), e.Enabled, e.Id)).ToArray();
        return Task.FromResult(ctx.Ok(new AdapterEnvListResult(items), CoveJsonContext.Default.AdapterEnvListResult));
    }

    [CoveCommand("cove://commands/adapter-env.save")]
    public static Task<ControlResponse> Save(EngineDispatchContext ctx)
    {
        if (ctx.AdapterEnv is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "adapter env store unavailable"));
        if (ctx.Request.Params is not System.Text.Json.JsonElement el)
            return Task.FromResult(ctx.Fail("invalid_params", "params required"));
        if (!el.TryGetProperty("adapter", out var adapterEl) || !el.TryGetProperty("entries", out var entriesEl))
            return Task.FromResult(ctx.Fail("invalid_params", "adapter and entries required"));
        var adapter = adapterEl.GetString();
        if (adapter is null || !LaunchProfileValidator.IsValidAdapter(adapter))
            return Task.FromResult(ctx.Fail("invalid_params", "valid adapter required"));

        var incoming = entriesEl.Deserialize(CoveJsonContext.Default.ListAdapterEnvVar) ?? new List<AdapterEnvVar>();
        var existing = store.Load(adapter);
        var existingById = existing.Where(e => e.Id is not null).ToDictionary(e => e.Id!);

        var merged = new List<AdapterEnvVar>();
        foreach (var entry in incoming)
        {
            if (entry.Id is not null && existingById.TryGetValue(entry.Id, out var stored) && entry.Value == MaskSentinel)
                merged.Add(entry with { Value = stored.Value });
            else
                merged.Add(entry);
        }

        store.Save(adapter, merged);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/adapter-env.resolve")]
    public static Task<ControlResponse> Resolve(EngineDispatchContext ctx)
    {
        if (ctx.AdapterEnv is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "adapter env store unavailable"));
        var adapter = ExtractAdapter(ctx);
        if (adapter is null)
            return Task.FromResult(ctx.Fail("invalid_params", "valid adapter required"));

        var entries = store.Load(adapter);
        var systemEnv = System.Environment.GetEnvironmentVariables();
        var sysDict = new Dictionary<string, string>();
        foreach (System.Collections.DictionaryEntry kv in systemEnv)
            sysDict[kv.Key.ToString()!] = kv.Value?.ToString() ?? "";
        var resolver = new EnvPrecedenceResolver(sysDict);
        var resolved = resolver.Resolve(adapter, entries);
        var masked = resolved.Select(kv => new ResolvedEnvVar(kv.Key, EnvVarParser.MaskSecret(kv.Key, kv.Value))).ToArray();
        return Task.FromResult(ctx.Ok(new AdapterEnvResolveResult(masked), CoveJsonContext.Default.AdapterEnvResolveResult));
    }

    private static string? ExtractAdapter(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not System.Text.Json.JsonElement el || !el.TryGetProperty("adapter", out var a))
            return null;
        var adapter = a.GetString();
        return adapter is not null && LaunchProfileValidator.IsValidAdapter(adapter) ? adapter : null;
    }
}
