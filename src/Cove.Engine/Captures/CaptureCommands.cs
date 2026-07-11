using System.Text.Json;
using Cove.Engine.Captures;
using Cove.Protocol;

namespace Cove.Engine.Captures;

public static class CaptureCommands
{
    [CoveCommand("cove://commands/capture.start")]
    public static Task<ControlResponse> Start(EngineDispatchContext ctx)
    {
        if (ctx.Captures is not { } store)
            return Task.FromResult(ctx.Fail("handler_error", "captures not available"));

        if (ctx.Request.Params is not JsonElement el)
            return Task.FromResult(ctx.Fail("handler_error", "missing params"));

        var bayId = el.TryGetProperty("bayId", out var ws) ? ws.GetString() ?? "" : "";
        var region = el.TryGetProperty("region", out var r) ? r.GetString() ?? "fullscreen" : "fullscreen";
        var audio = el.TryGetProperty("audio", out var a) && a.GetBoolean();
        var mic = el.TryGetProperty("mic", out var m) && m.GetBoolean();
        var cursor = el.TryGetProperty("cursor", out var c) && c.GetBoolean();

        var cap = store.StartCapture(bayId, region, audio, mic, cursor);
        return Task.FromResult(ctx.OkJson(SerializeCapture(cap)));
    }

    [CoveCommand("cove://commands/capture.stop")]
    public static Task<ControlResponse> Stop(EngineDispatchContext ctx)
    {
        if (ctx.Captures is not { } store)
            return Task.FromResult(ctx.Fail("handler_error", "captures not available"));

        if (ctx.Request.Params is not JsonElement el || !el.TryGetProperty("id", out var idEl))
            return Task.FromResult(ctx.Fail("handler_error", "missing id"));

        var cap = store.StopCapture(idEl.GetString() ?? "");
        if (cap is null)
            return Task.FromResult(ctx.Fail("handler_error", "capture not found"));

        return Task.FromResult(ctx.OkJson(SerializeCapture(cap)));
    }

    [CoveCommand("cove://commands/capture.list")]
    public static Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.Captures is not { } store)
            return Task.FromResult(ctx.Fail("handler_error", "captures not available"));

        var captures = store.ListCaptures();
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        var first = true;
        foreach (var cap in captures)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append(SerializeCapture(cap));
        }
        sb.Append(']');
        return Task.FromResult(ctx.OkJson(sb.ToString()));
    }

    [CoveCommand("cove://commands/capture.delete")]
    public static Task<ControlResponse> Delete(EngineDispatchContext ctx)
    {
        if (ctx.Captures is not { } store)
            return Task.FromResult(ctx.Fail("handler_error", "captures not available"));

        if (ctx.Request.Params is not JsonElement el || !el.TryGetProperty("id", out var idEl))
            return Task.FromResult(ctx.Fail("handler_error", "missing id"));

        var deleted = store.DeleteCapture(idEl.GetString() ?? "");
        return Task.FromResult(deleted ? ctx.Ok() : ctx.Fail("handler_error", "capture not found"));
    }

    [CoveCommand("cove://commands/capture.flag")]
    public static Task<ControlResponse> Flag(EngineDispatchContext ctx)
    {
        if (ctx.Captures is not { } store)
            return Task.FromResult(ctx.Fail("handler_error", "captures not available"));

        if (ctx.Request.Params is not JsonElement el || !el.TryGetProperty("id", out var idEl))
            return Task.FromResult(ctx.Fail("handler_error", "missing id"));

        var label = el.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
        store.FlagCapture(idEl.GetString() ?? "", label);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/capture.attach")]
    public static Task<ControlResponse> Attach(EngineDispatchContext ctx)
    {
        if (ctx.Captures is not { } store)
            return Task.FromResult(ctx.Fail("handler_error", "captures not available"));

        if (ctx.Request.Params is not JsonElement el || !el.TryGetProperty("captureId", out var capEl) || !el.TryGetProperty("taskId", out var taskEl))
            return Task.FromResult(ctx.Fail("handler_error", "missing captureId or taskId"));

        store.AttachToTask(capEl.GetString() ?? "", taskEl.GetString() ?? "");
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/capture.show")]
    public static Task<ControlResponse> Show(EngineDispatchContext ctx)
    {
        if (ctx.Captures is not { } store)
            return Task.FromResult(ctx.Fail("handler_error", "captures not available"));

        if (ctx.Request.Params is not JsonElement el || !el.TryGetProperty("id", out var idEl))
            return Task.FromResult(ctx.Fail("handler_error", "missing id"));

        var cap = store.GetCapture(idEl.GetString() ?? "");
        if (cap is null)
            return Task.FromResult(ctx.Fail("handler_error", "capture not found"));

        return Task.FromResult(ctx.OkJson(SerializeCapture(cap)));
    }

    private static string SerializeCapture(Capture cap)
    {
        return $$"""{"id":"{{cap.Id}}","number":{{cap.Number}},"bundleDir":"{{cap.BundleDir}}","bayId":"{{cap.BayId}}","region":"{{cap.Region}}","audio":{{cap.Audio.ToString().ToLowerInvariant()}},"mic":{{cap.Mic.ToString().ToLowerInvariant()}},"cursor":{{cap.Cursor.ToString().ToLowerInvariant()}},"createdAt":"{{cap.CreatedAt:o}}","durationMs":{{(long)cap.Duration.TotalMilliseconds}},"status":"{{cap.Status}}"}""";
    }
}
