using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Pty;

internal static partial class PtyDataLog
{
    [ZLoggerMessage(3000, LogLevel.Debug, "pty reader loop started nook={nookId} session={sessionId}")]
    public static partial void ReaderLoopStarted(this ILogger logger, string nookId, long sessionId);

    [ZLoggerMessage(3001, LogLevel.Information, "first output {bytes} bytes for nook {nookId}")]
    public static partial void ReaderFirstOutput(this ILogger logger, int bytes, string nookId);

    [ZLoggerMessage(3002, LogLevel.Trace, "pty read nook={nookId} bytes={bytes}")]
    public static partial void ReaderRead(this ILogger logger, string nookId, int bytes);

    [ZLoggerMessage(3003, LogLevel.Debug, "pty reader eof nook={nookId} totalBytes={totalBytes}")]
    public static partial void ReaderEof(this ILogger logger, string nookId, long totalBytes);

    [ZLoggerMessage(3004, LogLevel.Information, "pty reader exit nook={nookId} exitCode={exitCode}")]
    public static partial void ReaderExit(this ILogger logger, string nookId, int exitCode);

    [ZLoggerMessage(3005, LogLevel.Error, "pty reader read error nook={nookId} session={sessionId} errno={errno} error={error}")]
    public static partial void ReaderError(this ILogger logger, string nookId, long sessionId, int errno, string error);

    [ZLoggerMessage(3010, LogLevel.Debug, "first delivery nook={nookId} stream={streamId} offset={offset} bytes={bytes}")]
    public static partial void DeliveryFirst(this ILogger logger, string nookId, ulong streamId, ulong offset, int bytes);

    [ZLoggerMessage(3011, LogLevel.Trace, "stream delivery nook={nookId} stream={streamId} offset={offset} bytes={bytes}")]
    public static partial void DeliveryData(this ILogger logger, string nookId, ulong streamId, ulong offset, int bytes);

    [ZLoggerMessage(3012, LogLevel.Debug, "stream resync nook={nookId} stream={streamId} newBase={offset}")]
    public static partial void DeliveryResync(this ILogger logger, string nookId, ulong streamId, ulong offset);

    [ZLoggerMessage(3013, LogLevel.Debug, "stream end nook={nookId} stream={streamId} finalOffset={offset} exitCode={exitCode}")]
    public static partial void DeliveryEnd(this ILogger logger, string nookId, ulong streamId, ulong offset, int exitCode);

    [ZLoggerMessage(3014, LogLevel.Warning, "stream error nook={nookId} stream={streamId} code={code} message={message}")]
    public static partial void DeliveryError(this ILogger logger, string nookId, ulong streamId, string code, string message);

    [ZLoggerMessage(3020, LogLevel.Debug, "nook subscribe nook={nookId} baseOffset={baseOffset} head={head} tail={tail}")]
    public static partial void SubscribeStarted(this ILogger logger, string nookId, long baseOffset, long head, long tail);

    [ZLoggerMessage(3021, LogLevel.Warning, "nook subscribe unknown nook={nookId}")]
    public static partial void SubscribeUnknownNook(this ILogger logger, string nookId);

    [ZLoggerMessage(3022, LogLevel.Debug, "nook subscribe ended nook={nookId} ended={ended} faulted={faulted}")]
    public static partial void SubscribeEnded(this ILogger logger, string nookId, bool ended, bool faulted);

    [ZLoggerMessage(3023, LogLevel.Debug, "nook subscribe credit-loop closed nook={nookId} error={error}")]
    public static partial void SubscribeCreditLoopClosed(this ILogger logger, string nookId, string error);

    [ZLoggerMessage(3030, LogLevel.Debug, "nook write nook={nookId} bytes={bytes}")]
    public static partial void NookWrite(this ILogger logger, string nookId, int bytes);

    [ZLoggerMessage(3031, LogLevel.Warning, "nook write unknown nook={nookId}")]
    public static partial void NookWriteUnknown(this ILogger logger, string nookId);

    [ZLoggerMessage(3040, LogLevel.Information, "nook spawn nook={nookId} command={command} adapter={adapter} yolo={yolo} sessionIdPresent={sessionIdPresent} cols={cols} rows={rows}")]
    public static partial void NookSpawn(this ILogger logger, string nookId, string command, string adapter, bool yolo, bool sessionIdPresent, int cols, int rows);

    [ZLoggerMessage(3041, LogLevel.Debug, "nook spawn environment nook={nookId} envCount={envCount} argCount={argCount} cwd={cwd}")]
    public static partial void NookSpawnEnv(this ILogger logger, string nookId, int envCount, int argCount, string cwd);

    [ZLoggerMessage(3048, LogLevel.Warning, "nook spawn falling back to home dir nook={nookId} adapter={adapter} home={home} (no inherited, explicit, or bay cwd)")]
    public static partial void NookSpawnCwdFallback(this ILogger logger, string nookId, string adapter, string home);

    [ZLoggerMessage(3042, LogLevel.Information, "nook respawn nook={nookId} command={command} adapter={adapter}")]
    public static partial void NookRespawn(this ILogger logger, string nookId, string command, string adapter);

    [ZLoggerMessage(3043, LogLevel.Debug, "nook kill nook={nookId}")]
    public static partial void NookKill(this ILogger logger, string nookId);

    [ZLoggerMessage(3044, LogLevel.Warning, "nook kill unknown nook={nookId}")]
    public static partial void NookKillUnknown(this ILogger logger, string nookId);

    [ZLoggerMessage(3045, LogLevel.Warning, "nook stop signal failed nook={nookId} error={error}")]
    public static partial void NookStopFailed(this ILogger logger, string nookId, string error);

    [ZLoggerMessage(3046, LogLevel.Debug, "nook terminate step failed nook={nookId} step={step} error={error}")]
    public static partial void NookTerminateStepFailed(this ILogger logger, string nookId, string step, string error);

    [ZLoggerMessage(3047, LogLevel.Warning, "nook resize unknown nook={nookId}")]
    public static partial void NookResizeUnknown(this ILogger logger, string nookId);
}
