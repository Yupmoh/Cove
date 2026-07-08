using System.Text.Json;
using System.Threading.Tasks;
using Cove.Adapters;
using Cove.Protocol;

namespace Cove.Engine.Protocol;

internal static class ExtensionCommands
{
    [CoveCommand("cove://commands/extension.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.Extensions is not { } registry)
            return Task.FromResult(ctx.Fail("not_ready", "extension registry unavailable"));
        registry.Index();
        var commands = registry.List();
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var cmd in commands)
            {
                writer.WriteStartObject();
                writer.WriteString("command", cmd.Command);
                writer.WriteString("source", cmd.Source);
                writer.WriteString("adapter", cmd.Adapter);
                writer.WriteString("method", cmd.Method);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        return Task.FromResult(ctx.OkJson(System.Text.Encoding.UTF8.GetString(buffer.ToArray())));
    }

    [CoveCommand("cove://commands/extension.run")]
    public static async Task<ControlResponse> Run(EngineDispatchContext ctx)
    {
        if (ctx.Extensions is not { } registry)
            return ctx.Fail("not_ready", "extension registry unavailable");
        if (ctx.ManifestStore is not { } manifests)
            return ctx.Fail("not_ready", "manifest store unavailable");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ExtensionRunParams) is not { } p)
            return ctx.Fail("invalid_params", "extension run params required");
        var resolved = registry.Resolve(p.Command);
        if (resolved is null)
            return ctx.Fail("not_found", $"extension command '{p.Command}' not found");
        var manifest = manifests.Load(resolved.Adapter);
        if (manifest is null || !manifest.Methods.TryGetValue(resolved.Method, out var method) || method.Script is not { } script)
            return ctx.Fail("not_found", $"method '{resolved.Method}' not found in adapter '{resolved.Adapter}'");
        var runner = new MethodRunner();
        var args = new System.Collections.Generic.List<string>();
        if (p.Params is { } paramsJson)
            args.Add(paramsJson);
        var result = await runner.RunAsync(manifests.ResolveDir(resolved.Adapter), script, args, System.TimeSpan.FromMinutes(5)).ConfigureAwait(false);
        if (!result.Ok)
            return ctx.Fail("extension_failed", result.Stderr.Length > 0 ? result.Stderr : $"exit code {result.ExitCode}");
        return ctx.Ok(new ExtensionRunResult(result.Stdout), CoveJsonContext.Default.ExtensionRunResult);
    }
}
