using System.Text.Json.Serialization;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Daemon;

public static class DaemonCommands
{
    [CoveCommand("cove://commands/window.focus")]
    public static Task<ControlResponse> WindowFocus(
        EngineDispatchContext context)
    {
        var focused =
            context.ForwardWindowFocus?.Invoke(
                context.CancellationToken) == true;
        if (focused &&
            context.Bays?.ActiveBayId is { } activeBayId)
        {
            _ = context.Bays.RefreshWorktreesAsync(activeBayId);
        }
        return Task.FromResult(
            context.Ok(
                new WindowFocusResult(
                    focused,
                    focused ? null : "no_render_client"),
                DaemonCommandsJsonContext.Default
                    .WindowFocusResult));
    }

    [CoveCommand("cove://commands/restore.summary.get")]
    public static Task<ControlResponse> RestoreSummaryGet(
        EngineDispatchContext context)
    {
        var summary = context.GetRestorationSummary?.Invoke();
        var result = new Cove.Engine.Restart.RestoreSummaryPullResult(
            summary is not null,
            summary?.Restored ?? 0,
            summary?.Fresh ?? 0,
            summary?.Skipped ?? 0,
            summary?.BootedAt ??
            context.EngineStartedAtUtc?.ToString("O") ??
            string.Empty);
        return Task.FromResult(
            context.Ok(
                result,
                Cove.Engine.Restart.RestorationSummaryJsonContext
                    .Default.RestoreSummaryPullResult));
    }
}

public sealed record WindowFocusResult(
    bool Focused,
    [property: JsonIgnore(
        Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Reason);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WindowFocusResult))]
internal sealed partial class DaemonCommandsJsonContext
    : JsonSerializerContext
{
}
