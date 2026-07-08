using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

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
    private const int MaxDumpSizeBytes = 10 * 1024 * 1024;
    private const int MaxAgeDays = 7;

    private static readonly Regex[] RedactionPatterns =
    [
        new Regex(@"(/Users/|/home/|C:\\Users\\|/root/)[^\s""',)]+", RegexOptions.Compiled),
        new Regex(@"\\Users\\[^\s""',)]+", RegexOptions.Compiled),
    ];

    public static bool Registered { get; private set; }

    public CrashReporter(string dataDir, ILogger logger)
    {
        _diagnosticsDir = System.IO.Path.Combine(dataDir, "diagnostics");
        System.IO.Directory.CreateDirectory(_diagnosticsDir);
        _logger = logger;
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
        var at = System.DateTimeOffset.UtcNow;
        var fileName = $"crash-{at:yyyyMMdd-HHmmss}-{id[..8]}.json";
        var filePath = System.IO.Path.Combine(_diagnosticsDir, fileName);

        var redactedMessage = Redact(message);
        var redactedStack = Redact(stackTrace ?? "");

        var record = new CrashDumpRecord(id, processName, exceptionType, redactedMessage, at.ToString("o"), redactedStack);
        var json = JsonSerializer.Serialize(record, CrashJsonContext.Default.CrashDumpRecord);
        System.IO.File.WriteAllText(filePath, json);

        _logger.LogWarning("crash: recorded {id} ({type}) to {path}", id, exceptionType, filePath);

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
                    _logger.LogWarning("crash: skipping unparseable dump at {path}", file);
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
                _logger.LogWarning(ex, "crash: failed to read dump at {path}", file);
            }
        }
        return result.OrderByDescending(c => c.At).ToList();
    }

    public void Prune()
    {
        if (!System.IO.Directory.Exists(_diagnosticsDir)) return;

        var cutoff = System.DateTimeOffset.UtcNow.AddDays(-MaxAgeDays);
        long totalSize = 0;
        var files = System.IO.Directory.EnumerateFiles(_diagnosticsDir, "crash-*.json")
            .Select(f => new System.IO.FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .ToList();

        foreach (var file in files)
        {
            totalSize += file.Length;
            var fileAge = System.DateTimeOffset.UtcNow - file.CreationTimeUtc;
            if (fileAge.TotalDays > MaxAgeDays || totalSize > MaxDumpSizeBytes)
            {
                try { file.Delete(); }
                catch (System.Exception ex) { _logger.LogWarning(ex, "crash: failed to prune dump at {path}", file.FullName); }
            }
        }
    }

    public bool DeleteCrash(string id)
    {
        var crashes = ListCrashes();
        var crash = crashes.FirstOrDefault(c => c.Id == id);
        if (crash is null) return false;
        try { System.IO.File.Delete(crash.FilePath); return true; }
        catch (System.Exception ex) { _logger.LogWarning(ex, "crash: failed to delete dump {id} at {path}", id, crash.FilePath); return false; }
    }

    internal static string Redact(string input)
    {
        var result = input;
        foreach (var pattern in RedactionPatterns)
            result = pattern.Replace(result, "[REDACTED]");
        return result;
    }
}
