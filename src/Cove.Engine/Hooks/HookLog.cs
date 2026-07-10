using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Hooks;

internal static partial class HookLog
{
    [ZLoggerMessage(LogLevel.Warning, "hook server error error={error}")]
    public static partial void HookServerError(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "hook handler failed adapter={adapter} event={eventName} error={error}")]
    public static partial void HookHandlerFailed(this ILogger logger, string adapter, string eventName, string error);

    [ZLoggerMessage(LogLevel.Warning, "hook payload invalid adapter={adapter} event={eventName} error={error}")]
    public static partial void HookPayloadInvalid(this ILogger logger, string adapter, string eventName, string error);

    [ZLoggerMessage(LogLevel.Warning, "hook listener error error={error}")]
    public static partial void HookListenError(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "hook request handler error error={error}")]
    public static partial void HookRequestError(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "hook event dropped no pane-id adapter={adapter} event={eventName}")]
    public static partial void HookEventNoPaneId(this ILogger logger, string adapter, string eventName);

    [ZLoggerMessage(LogLevel.Warning, "hook event unknown adapter={adapter} event={eventName}")]
    public static partial void HookEventUnknown(this ILogger logger, string adapter, string eventName);

    [ZLoggerMessage(LogLevel.Warning, "hook event dropped untracked pane adapter={adapter} event={eventName} pane={paneId}")]
    public static partial void HookEventUntrackedPane(this ILogger logger, string adapter, string eventName, string paneId);

    [ZLoggerMessage(LogLevel.Warning, "hook resolve missing params adapter={adapter} event={eventName}")]
    public static partial void HookResolveMissingParams(this ILogger logger, string adapter, string eventName);

    [ZLoggerMessage(LogLevel.Warning, "hook matrix manifest load failed path={path} error={error}")]
    public static partial void HookMatrixManifestLoadFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "ambient run-command enumeration failed error={error}")]
    public static partial void AmbientRunCommandFailed(this ILogger logger, string error);
    [ZLoggerMessage(LogLevel.Warning, "context injection failed adapter={adapter} event={eventName} error={error}")]
    public static partial void ContextInjectionFailed(this ILogger logger, string adapter, string eventName, string error);
}
