using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Gui;

internal static partial class GuiLog
{
    [ZLoggerMessage(LogLevel.Information, "gui starting channel={channel} version={version} url={url} enginePath={enginePath}")]
    public static partial void AppStarting(this ILogger logger, string channel, string version, string url, string enginePath);

    [ZLoggerMessage(LogLevel.Information, "loopback server started port={port}")]
    public static partial void LoopbackServerStarted(this ILogger logger, int port);

    [ZLoggerMessage(LogLevel.Error, "loopback connection handler failed error={error}")]
    public static partial void LoopbackConnectionHandlerFailed(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "loopback media not found path={path}")]
    public static partial void LoopbackMediaNotFound(this ILogger logger, string path);

    [ZLoggerMessage(LogLevel.Warning, "loopback media lease rejected lease={lease}")]
    public static partial void LoopbackMediaLeaseRejected(this ILogger logger, string lease);

    [ZLoggerMessage(LogLevel.Warning, "media lease issue rejected path={path} error={error}")]
    public static partial void MediaLeaseIssueRejected(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "daemon control token missing path={path}")]
    public static partial void ControlTokenMissing(this ILogger logger, string path);

    [ZLoggerMessage(LogLevel.Warning, "daemon control token read failed error={error}")]
    public static partial void ControlTokenReadFailed(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "loopback request rejected path={path}")]
    public static partial void LoopbackRequestRejected(this ILogger logger, string path);

    [ZLoggerMessage(LogLevel.Error, "pty websocket relay failed nookId={nookId} error={error}")]
    public static partial void PtyWebSocketRelayFailed(this ILogger logger, string nookId, string error);

    [ZLoggerMessage(LogLevel.Information, "gui window built channel={channel} devtools={devtools}")]
    public static partial void WindowBuilt(this ILogger logger, string channel, bool devtools);

    [ZLoggerMessage(LogLevel.Information, "engine link connecting channel={channel} endpoint={endpoint}")]
    public static partial void EngineConnecting(this ILogger logger, string channel, string endpoint);

    [ZLoggerMessage(LogLevel.Information, "engine link connected channel={channel} endpoint={endpoint} engineVersion={engineVersion}")]
    public static partial void EngineConnected(this ILogger logger, string channel, string endpoint, string engineVersion);

    [ZLoggerMessage(LogLevel.Warning, "engine link reconnecting channel={channel} endpoint={endpoint}")]
    public static partial void EngineReconnecting(this ILogger logger, string channel, string endpoint);

    [ZLoggerMessage(LogLevel.Error, "engine hello rejected channel={channel} endpoint={endpoint} code={code}")]
    public static partial void EngineHelloRejected(this ILogger logger, string channel, string endpoint, string code);

    [ZLoggerMessage(LogLevel.Trace, "engine request uri={uri} durationMs={durationMs}")]
    public static partial void EngineRequest(this ILogger logger, string uri, long durationMs);

    [ZLoggerMessage(LogLevel.Error, "engine request failed uri={uri} error={error}")]
    public static partial void EngineRequestFailed(this ILogger logger, string uri, string error);

    [ZLoggerMessage(LogLevel.Warning, "engine control error frame code={code} message={message} streamId={streamId}")]
    public static partial void EngineControlError(this ILogger logger, string code, string message, string streamId);

    [ZLoggerMessage(LogLevel.Warning, "engine event forward failed error={error}")]
    public static partial void EngineEventDeserializeFailed(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Warning, "engine read pump ended error={error}")]
    public static partial void EngineReadPumpEnded(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Debug, "engine dial attempt channel={channel} target={target}")]
    public static partial void LauncherDialAttempt(this ILogger logger, string channel, string target);

    [ZLoggerMessage(LogLevel.Debug, "engine dial failed channel={channel} target={target} error={error}")]
    public static partial void LauncherDialFailed(this ILogger logger, string channel, string target, string error);

    [ZLoggerMessage(LogLevel.Information, "engine spawn channel={channel} exe={exe}")]
    public static partial void LauncherSpawn(this ILogger logger, string channel, string exe);

    [ZLoggerMessage(LogLevel.Error, "engine spawn timeout channel={channel} target={target} waitedMs={waitedMs}")]
    public static partial void LauncherSpawnTimeout(this ILogger logger, string channel, string target, long waitedMs);

    [ZLoggerMessage(LogLevel.Trace, "gui command uri={uri}")]
    public static partial void CommandInvoked(this ILogger logger, string uri);

    [ZLoggerMessage(LogLevel.Error, "gui command engine error uri={uri} error={error}")]
    public static partial void CommandEngineFailed(this ILogger logger, string uri, string error);

    [ZLoggerMessage(LogLevel.Warning, "gui feedback save failed slug={slug} error={error}")]
    public static partial void FeedbackSaveFailed(this ILogger logger, string slug, string error);

    [ZLoggerMessage(LogLevel.Warning, "engine event dropped no window channel={channel}")]
    public static partial void EventForwardNoWindow(this ILogger logger, string channel);

    [ZLoggerMessage(LogLevel.Warning, "engine event dropped no webview channel={channel}")]
    public static partial void EventForwardNoWebView(this ILogger logger, string channel);

    [ZLoggerMessage(2074, LogLevel.Warning, "dictation model ensure failed error={error}")]
    public static partial void DictationEnsureFailed(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Trace, "engine event forwarded channel={channel}")]
    public static partial void EventForwarded(this ILogger logger, string channel);

    [ZLoggerMessage(LogLevel.Error, "[webview] {message}")]
    public static partial void FrontendError(this ILogger logger, string message);

    [ZLoggerMessage(LogLevel.Warning, "[webview] {message}")]
    public static partial void FrontendWarn(this ILogger logger, string message);

    [ZLoggerMessage(LogLevel.Information, "[webview] {message}")]
    public static partial void FrontendInfo(this ILogger logger, string message);
}
