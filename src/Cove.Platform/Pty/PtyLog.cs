using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Platform.Pty;

internal static partial class PtyLog
{
    [ZLoggerMessage(LogLevel.Trace, "conpty spawn begin command={command} cwd={cwd} cols={cols} rows={rows}", EventId = 1000)]
    public static partial void WinSpawnBegin(this ILogger logger, string command, string? cwd, int cols, int rows);

    [ZLoggerMessage(LogLevel.Trace, "conpty pipe created kind={kind} valid={valid}", EventId = 1001)]
    public static partial void WinPipeCreated(this ILogger logger, string kind, bool valid);

    [ZLoggerMessage(LogLevel.Error, "conpty pipe create failed stage={stage} command={command} error={error}", EventId = 1002)]
    public static partial void WinPipeCreateFailed(this ILogger logger, string stage, string command, int error);

    [ZLoggerMessage(LogLevel.Trace, "conpty CreatePseudoConsole result hr=0x{hr:X8} handleValid={handleValid}", EventId = 1003)]
    public static partial void WinPseudoConsoleCreated(this ILogger logger, int hr, bool handleValid);

    [ZLoggerMessage(LogLevel.Error, "conpty CreatePseudoConsole failed command={command} hr=0x{hr:X8}", EventId = 1004)]
    public static partial void WinPseudoConsoleFailed(this ILogger logger, string command, int hr);

    [ZLoggerMessage(LogLevel.Trace, "conpty attribute list initialized bytes={bytes}", EventId = 1005)]
    public static partial void WinAttributeListInitialized(this ILogger logger, long bytes);

    [ZLoggerMessage(LogLevel.Error, "conpty InitializeProcThreadAttributeList failed command={command} error={error}", EventId = 1006)]
    public static partial void WinAttributeListInitFailed(this ILogger logger, string command, int error);

    [ZLoggerMessage(LogLevel.Trace, "conpty attribute list updated with pseudoconsole command={command}", EventId = 1007)]
    public static partial void WinAttributeListUpdated(this ILogger logger, string command);

    [ZLoggerMessage(LogLevel.Error, "conpty UpdateProcThreadAttribute failed command={command} error={error}", EventId = 1008)]
    public static partial void WinAttributeListUpdateFailed(this ILogger logger, string command, int error);

    [ZLoggerMessage(LogLevel.Trace, "conpty CreateProcessW begin commandLineLength={commandLineLength} creationFlags=0x{creationFlags:X8}", EventId = 1009)]
    public static partial void WinCreateProcessBegin(this ILogger logger, int commandLineLength, uint creationFlags);

    [ZLoggerMessage(LogLevel.Trace, "conpty CreateProcessW succeeded command={command} pid={pid}", EventId = 1010)]
    public static partial void WinCreateProcessSucceeded(this ILogger logger, string command, int pid);

    [ZLoggerMessage(LogLevel.Error, "conpty CreateProcessW failed command={command} error={error}", EventId = 1011)]
    public static partial void WinCreateProcessFailed(this ILogger logger, string command, int error);

    [ZLoggerMessage(LogLevel.Trace, "conpty exit watcher started session={sessionId}", EventId = 1012)]
    public static partial void WinExitWatcherStarted(this ILogger logger, long sessionId);

    [ZLoggerMessage(LogLevel.Debug, "conpty first read session={sessionId} bytes={bytes}", EventId = 1013)]
    public static partial void WinFirstRead(this ILogger logger, long sessionId, int bytes);

    [ZLoggerMessage(LogLevel.Trace, "conpty read session={sessionId} bytes={bytes}", EventId = 1014)]
    public static partial void WinRead(this ILogger logger, long sessionId, int bytes);

    [ZLoggerMessage(LogLevel.Trace, "conpty read reached eof session={sessionId} error={error}", EventId = 1015)]
    public static partial void WinReadEof(this ILogger logger, long sessionId, int error);

    [ZLoggerMessage(LogLevel.Error, "conpty read failed session={sessionId} error={error}", EventId = 1016)]
    public static partial void WinReadFailed(this ILogger logger, long sessionId, int error);

    [ZLoggerMessage(LogLevel.Trace, "conpty write begin session={sessionId} bytes={bytes}", EventId = 1017)]
    public static partial void WinWriteBegin(this ILogger logger, long sessionId, int bytes);

    [ZLoggerMessage(LogLevel.Trace, "conpty write chunk session={sessionId} written={written} offset={offset}", EventId = 1018)]
    public static partial void WinWriteChunk(this ILogger logger, long sessionId, int written, int offset);

    [ZLoggerMessage(LogLevel.Warning, "conpty write made no progress session={sessionId} offset={offset}", EventId = 1019)]
    public static partial void WinWriteNoProgress(this ILogger logger, long sessionId, int offset);

    [ZLoggerMessage(LogLevel.Error, "conpty write failed session={sessionId} offset={offset} error={error}", EventId = 1020)]
    public static partial void WinWriteFailed(this ILogger logger, long sessionId, int offset, int error);

    [ZLoggerMessage(LogLevel.Trace, "conpty resize requested session={sessionId} cols={cols} rows={rows}", EventId = 1021)]
    public static partial void WinResizeRequested(this ILogger logger, long sessionId, int cols, int rows);

    [ZLoggerMessage(LogLevel.Warning, "conpty resize clamped session={sessionId} requested={cols}x{rows} applied={c}x{r}", EventId = 1022)]
    public static partial void WinResizeClamped(this ILogger logger, long sessionId, int cols, int rows, int c, int r);

    [ZLoggerMessage(LogLevel.Trace, "conpty resize skipped console already closed session={sessionId}", EventId = 1023)]
    public static partial void WinResizeSkippedClosed(this ILogger logger, long sessionId);

    [ZLoggerMessage(LogLevel.Warning, "conpty resize failed session={sessionId} hr=0x{hr:X8}", EventId = 1024)]
    public static partial void WinResizeFailed(this ILogger logger, long sessionId, int hr);

    [ZLoggerMessage(LogLevel.Trace, "conpty kill requested session={sessionId}", EventId = 1025)]
    public static partial void WinKillRequested(this ILogger logger, long sessionId);

    [ZLoggerMessage(LogLevel.Warning, "conpty kill failed session={sessionId} error={error}", EventId = 1026)]
    public static partial void WinKillFailed(this ILogger logger, long sessionId, int error);

    [ZLoggerMessage(LogLevel.Warning, "conpty signal unsupported session={sessionId} signum={signum}", EventId = 1027)]
    public static partial void WinSignalUnsupported(this ILogger logger, long sessionId, int signum);

    [ZLoggerMessage(LogLevel.Trace, "conpty exit observed session={sessionId} exitCode={exitCode}", EventId = 1028)]
    public static partial void WinExitObserved(this ILogger logger, long sessionId, int exitCode);

    [ZLoggerMessage(LogLevel.Warning, "conpty GetExitCodeProcess failed session={sessionId} error={error}", EventId = 1029)]
    public static partial void WinGetExitCodeFailed(this ILogger logger, long sessionId, int error);

    [ZLoggerMessage(LogLevel.Trace, "conpty ClosePseudoConsole session={sessionId}", EventId = 1030)]
    public static partial void WinPseudoConsoleClosed(this ILogger logger, long sessionId);

    [ZLoggerMessage(LogLevel.Trace, "conpty dispose begin session={sessionId} hasExited={hasExited}", EventId = 1031)]
    public static partial void WinDisposeBegin(this ILogger logger, long sessionId, bool hasExited);

    [ZLoggerMessage(LogLevel.Trace, "conpty dispose closed process and thread handles session={sessionId}", EventId = 1032)]
    public static partial void WinDisposeHandlesClosed(this ILogger logger, long sessionId);

    [ZLoggerMessage(LogLevel.Trace, "pty unix spawn begin command={command} cwd={cwd} cols={cols} rows={rows}", EventId = 1050)]
    public static partial void UnixSpawnBegin(this ILogger logger, string command, string? cwd, int cols, int rows);

    [ZLoggerMessage(LogLevel.Trace, "pty native abi ok version={version}", EventId = 1051)]
    public static partial void UnixAbiOk(this ILogger logger, int version);

    [ZLoggerMessage(LogLevel.Error, "pty native abi mismatch got={got} expected={expected}", EventId = 1052)]
    public static partial void UnixAbiMismatch(this ILogger logger, int got, int expected);

    [ZLoggerMessage(LogLevel.Error, "pty native shim not found next to the binary error={error}", EventId = 1053)]
    public static partial void UnixNativeLibNotFound(this ILogger logger, string error);

    [ZLoggerMessage(LogLevel.Trace, "pty executable resolved command={command} path={path}", EventId = 1054)]
    public static partial void UnixExecutableResolved(this ILogger logger, string command, string path);

    [ZLoggerMessage(LogLevel.Error, "pty executable not found command={command} reason={reason}", EventId = 1055)]
    public static partial void UnixExecutableNotFound(this ILogger logger, string command, string reason);

    [ZLoggerMessage(LogLevel.Trace, "pty environment built entries={entries}", EventId = 1056)]
    public static partial void UnixEnvironmentBuilt(this ILogger logger, int entries);

    [ZLoggerMessage(LogLevel.Trace, "pty forkpty begin path={path}", EventId = 1057)]
    public static partial void UnixForkptyBegin(this ILogger logger, string path);

    [ZLoggerMessage(LogLevel.Error, "pty forkpty failed path={path} errno={errno}", EventId = 1058)]
    public static partial void UnixForkptyFailed(this ILogger logger, string path, int errno);

    [ZLoggerMessage(LogLevel.Trace, "pty master fd ready session={sessionId} fdValid={fdValid}", EventId = 1059)]
    public static partial void UnixMasterFdReady(this ILogger logger, long sessionId, bool fdValid);

    [ZLoggerMessage(LogLevel.Debug, "pty first read session={sessionId} bytes={bytes}", EventId = 1060)]
    public static partial void UnixFirstRead(this ILogger logger, long sessionId, int bytes);

    [ZLoggerMessage(LogLevel.Trace, "pty read session={sessionId} bytes={bytes}", EventId = 1061)]
    public static partial void UnixRead(this ILogger logger, long sessionId, int bytes);

    [ZLoggerMessage(LogLevel.Error, "pty read failed session={sessionId} errno={errno}", EventId = 1062)]
    public static partial void UnixReadFailed(this ILogger logger, long sessionId, int errno);

    [ZLoggerMessage(LogLevel.Trace, "pty write session={sessionId} bytes={bytes}", EventId = 1063)]
    public static partial void UnixWrite(this ILogger logger, long sessionId, int bytes);

    [ZLoggerMessage(LogLevel.Error, "pty write failed session={sessionId} bytes={bytes} errno={errno}", EventId = 1064)]
    public static partial void UnixWriteFailed(this ILogger logger, long sessionId, int bytes, int errno);

    [ZLoggerMessage(LogLevel.Trace, "pty resize requested session={sessionId} cols={cols} rows={rows}", EventId = 1065)]
    public static partial void UnixResizeRequested(this ILogger logger, long sessionId, int cols, int rows);

    [ZLoggerMessage(LogLevel.Warning, "pty resize clamped session={sessionId} requested={cols}x{rows} applied={c}x{r}", EventId = 1066)]
    public static partial void UnixResizeClamped(this ILogger logger, long sessionId, int cols, int rows, int c, int r);

    [ZLoggerMessage(LogLevel.Warning, "pty resize ioctl failed session={sessionId} errno={errno}", EventId = 1067)]
    public static partial void UnixResizeFailed(this ILogger logger, long sessionId, int errno);

    [ZLoggerMessage(LogLevel.Trace, "pty kill requested session={sessionId}", EventId = 1068)]
    public static partial void UnixKillRequested(this ILogger logger, long sessionId);

    [ZLoggerMessage(LogLevel.Warning, "pty kill failed session={sessionId} errno={errno}", EventId = 1069)]
    public static partial void UnixKillFailed(this ILogger logger, long sessionId, int errno);

    [ZLoggerMessage(LogLevel.Trace, "pty signal session={sessionId} signum={signum}", EventId = 1070)]
    public static partial void UnixSignalRequested(this ILogger logger, long sessionId, int signum);

    [ZLoggerMessage(LogLevel.Warning, "pty signal failed session={sessionId} signum={signum} errno={errno}", EventId = 1071)]
    public static partial void UnixSignalFailed(this ILogger logger, long sessionId, int signum, int errno);

    [ZLoggerMessage(LogLevel.Trace, "pty wait for exit begin session={sessionId}", EventId = 1072)]
    public static partial void UnixWaitBegin(this ILogger logger, long sessionId);

    [ZLoggerMessage(LogLevel.Trace, "pty reaped session={sessionId} exitCode={exitCode}", EventId = 1073)]
    public static partial void UnixReaped(this ILogger logger, long sessionId, int exitCode);

    [ZLoggerMessage(LogLevel.Warning, "pty reap timed out session={sessionId} pid={pid}", EventId = 1074)]
    public static partial void UnixReapTimeout(this ILogger logger, long sessionId, int pid);

    [ZLoggerMessage(LogLevel.Trace, "pty dispose closing master fd session={sessionId}", EventId = 1075)]
    public static partial void UnixDisposeClose(this ILogger logger, long sessionId);

    [ZLoggerMessage(LogLevel.Information, "pty session spawned session={sessionId} command={command} cwd={cwd} pid={pid} cols={cols} rows={rows}", EventId = 1090)]
    public static partial void SessionSpawned(this ILogger logger, long sessionId, string command, string? cwd, int pid, int cols, int rows);

    [ZLoggerMessage(LogLevel.Information, "pty session exited session={sessionId} exitCode={exitCode}", EventId = 1091)]
    public static partial void SessionExited(this ILogger logger, long sessionId, int exitCode);
}
