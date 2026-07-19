using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Engine.Diagnostics;

public sealed record CrashDump(string Id, string ProcessName, string ExceptionType, string Message, System.DateTimeOffset At, string FilePath);
public sealed record CrashDumpRecord(string Id, string Process, string ExceptionType, string Message, string At, string StackTrace);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(CrashDumpRecord))]
public sealed partial class CrashJsonContext : JsonSerializerContext { }

public sealed class CrashReporter
{
    private readonly string _diagnosticsDir;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly System.Func<System.IO.FileInfo, long> _fileLength;
    private const int MaxDumpSizeBytes = 10 * 1024 * 1024;
    private const int MaxAgeDays = 7;

    private static readonly Regex[] RedactionPatterns =
    [
        new Regex(@"(/Users/|/home/|C:\\Users\\|/root/)[^\s""',)]+", RegexOptions.Compiled),
        new Regex(@"\\Users\\[^\s""',)]+", RegexOptions.Compiled),
    ];

    public static bool Registered { get; private set; }

    public CrashReporter(string dataDir, ILogger logger, TimeProvider? timeProvider = null)
        : this(dataDir, logger, timeProvider, static file => file.Length)
    {
    }

    internal CrashReporter(
        string dataDir,
        ILogger logger,
        TimeProvider? timeProvider,
        System.Func<System.IO.FileInfo, long> fileLength)
    {
        _diagnosticsDir = System.IO.Path.Combine(dataDir, "diagnostics");
        System.IO.Directory.CreateDirectory(_diagnosticsDir);
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _fileLength = fileLength;
    }

    public void Register()
    {
        if (Registered) return;

        System.AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Registered = true;
    }

    private void OnUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is System.Exception ex)
            HandleUnhandledException(ex, System.Diagnostics.Process.GetCurrentProcess().ProcessName);
    }

    private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        HandleUnhandledException(e.Exception, System.Diagnostics.Process.GetCurrentProcess().ProcessName);
    }

    public void HandleUnhandledException(System.Exception exception, string processName)
    {
        var exceptionType = exception.GetType().Name;
        var message = exception.Message;
        var stackTrace = exception.StackTrace;
        RecordCrash(processName, exceptionType, message, stackTrace);
    }

    public CrashDump RecordCrash(string processName, string exceptionType, string message, string? stackTrace)
    {
        var id = System.Guid.NewGuid().ToString("N");
        var at = _timeProvider.GetUtcNow();
        var fileName = $"crash-{at:yyyyMMdd-HHmmss}-{id[..8]}.json";
        var filePath = System.IO.Path.Combine(_diagnosticsDir, fileName);

        var redactedMessage = Redact(message);
        var redactedStack = Redact(stackTrace ?? "");

        var record = new CrashDumpRecord(id, processName, exceptionType, redactedMessage, at.ToString("o"), redactedStack);
        var json = JsonSerializer.Serialize(record, CrashJsonContext.Default.CrashDumpRecord);
        System.IO.File.WriteAllText(filePath, json);

        _logger.CrashRecorded(id, exceptionType, filePath);

        Prune();
        return new CrashDump(id, processName, exceptionType, redactedMessage, at, filePath);
    }

    public System.Collections.Generic.IReadOnlyList<CrashDump> ListCrashes()
    {
        var result = new System.Collections.Generic.List<CrashDump>();
        if (!System.IO.Directory.Exists(_diagnosticsDir)) return result;

        foreach (var file in System.IO.Directory.EnumerateFiles(_diagnosticsDir, "crash-*.json"))
        {
            try
            {
                var json = System.IO.File.ReadAllText(file);
                var record = JsonSerializer.Deserialize(json, CrashJsonContext.Default.CrashDumpRecord);
                if (record is null)
                {
                    _logger.CrashUnparseable(file);
                    continue;
                }
                result.Add(new CrashDump(
                    record.Id,
                    record.Process,
                    record.ExceptionType,
                    record.Message,
                    System.DateTimeOffset.Parse(record.At, null, System.Globalization.DateTimeStyles.RoundtripKind),
                    file
                ));
            }
            catch (System.Exception ex)
            {
                _logger.CrashReadFailed(file, ex.Message);
            }
        }
        return result.OrderByDescending(c => c.At).ToList();
    }

    public void Prune()
    {
        if (!System.IO.Directory.Exists(_diagnosticsDir)) return;

        var cutoff = _timeProvider.GetUtcNow().AddDays(-MaxAgeDays);
        long totalSize = 0;
        var files = new System.Collections.Generic.List<CrashFile>();

        foreach (var path in System.IO.Directory.EnumerateFiles(_diagnosticsDir, "crash-*.json"))
        {
            var file = new System.IO.FileInfo(path);
            var length = ReadLength(file);
            files.Add(new CrashFile(file, ReadCrashTime(file), length));
        }

        foreach (var crashFile in files.OrderByDescending(file => file.At).ThenBy(file => file.File.FullName, System.StringComparer.Ordinal))
        {
            if (crashFile.At < cutoff)
            {
                if (!TryPrune(crashFile.File))
                    totalSize = AddSize(totalSize, crashFile.Length);
                continue;
            }

            var nextSize = AddSize(totalSize, crashFile.Length);
            if (nextSize > MaxDumpSizeBytes)
            {
                if (!TryPrune(crashFile.File))
                    totalSize = nextSize;
                continue;
            }

            totalSize = nextSize;
        }
    }

    private long ReadLength(System.IO.FileInfo file)
    {
        try
        {
            return _fileLength(file);
        }
        catch (System.Exception ex)
        {
            _logger.CrashMetadataReadFailed(file.FullName, ex.Message);
            return long.MaxValue;
        }
    }

    private System.DateTimeOffset ReadCrashTime(System.IO.FileInfo file)
    {
        try
        {
            using var stream = file.OpenRead();
            var record = JsonSerializer.Deserialize(stream, CrashJsonContext.Default.CrashDumpRecord);
            if (record is not null &&
                System.DateTimeOffset.TryParse(
                    record.At,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var persistedAt))
                return persistedAt;

            _logger.CrashUnparseable(file.FullName);
        }
        catch (System.Exception ex)
        {
            _logger.CrashReadFailed(file.FullName, ex.Message);
        }

        try
        {
            return file.LastWriteTimeUtc;
        }
        catch (System.Exception ex)
        {
            _logger.CrashMetadataReadFailed(file.FullName, ex.Message);
            return System.DateTimeOffset.MinValue;
        }
    }

    private bool TryPrune(System.IO.FileInfo file)
    {
        try
        {
            file.Delete();
            return true;
        }
        catch (System.Exception ex)
        {
            _logger.CrashPruneFailed(file.FullName, ex.Message);
            return false;
        }
    }

    private static long AddSize(long totalSize, long length) =>
        length > long.MaxValue - totalSize ? long.MaxValue : totalSize + length;

    public bool DeleteCrash(string id)
    {
        var crashes = ListCrashes();
        var crash = crashes.FirstOrDefault(c => c.Id == id);
        if (crash is null) return false;
        try { System.IO.File.Delete(crash.FilePath); return true; }
        catch (System.Exception ex) { _logger.CrashDeleteFailed(id, crash.FilePath, ex.Message); return false; }
    }

    internal static string Redact(string input)
    {
        var result = input;
        foreach (var pattern in RedactionPatterns)
            result = pattern.Replace(result, "[REDACTED]");
        return result;
    }

    private readonly record struct CrashFile(System.IO.FileInfo File, System.DateTimeOffset At, long Length);
}

internal static partial class CrashReporterLog
{
    [ZLoggerMessage(LogLevel.Warning, "crash recorded id={id} type={type} path={path}")]
    public static partial void CrashRecorded(this ILogger logger, string id, string type, string path);

    [ZLoggerMessage(LogLevel.Warning, "crash dump unparseable path={path}")]
    public static partial void CrashUnparseable(this ILogger logger, string path);

    [ZLoggerMessage(LogLevel.Warning, "crash dump read failed path={path} error={error}")]
    public static partial void CrashReadFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "crash dump metadata read failed path={path} error={error}")]
    public static partial void CrashMetadataReadFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "crash dump prune failed path={path} error={error}")]
    public static partial void CrashPruneFailed(this ILogger logger, string path, string error);

    [ZLoggerMessage(LogLevel.Warning, "crash dump delete failed id={id} path={path} error={error}")]
    public static partial void CrashDeleteFailed(this ILogger logger, string id, string path, string error);
}
