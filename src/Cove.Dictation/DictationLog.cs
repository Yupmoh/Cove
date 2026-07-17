using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Dictation;

internal static partial class DictationLog
{
    [ZLoggerMessage(3500, LogLevel.Information, "dictation recording started device={device}")]
    public static partial void DictationRecordingStarted(this ILogger logger, string device);

    [ZLoggerMessage(3501, LogLevel.Information, "dictation clip skipped seconds={seconds} reason={reason}")]
    public static partial void DictationClipSkipped(this ILogger logger, double seconds, string reason);

    [ZLoggerMessage(3502, LogLevel.Information, "dictation transcribed audioSeconds={audioSeconds} speechSeconds={speechSeconds} ms={ms} chars={chars}")]
    public static partial void DictationTranscribed(this ILogger logger, double audioSeconds, double speechSeconds, long ms, int chars);

    [ZLoggerMessage(3503, LogLevel.Information, "dictation model download started url={url}")]
    public static partial void DictationModelDownloadStarted(this ILogger logger, string url);

    [ZLoggerMessage(3504, LogLevel.Information, "dictation model ready dir={dir}")]
    public static partial void DictationModelReady(this ILogger logger, string dir);

    [ZLoggerMessage(3505, LogLevel.Warning, "dictation model checksum mismatch expected={expected} actual={actual}")]
    public static partial void DictationChecksumMismatch(this ILogger logger, string expected, string actual);
}
