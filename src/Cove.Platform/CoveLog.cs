using System.IO;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Platform;

public static class CoveLog
{
    public static ILoggerFactory CreateEngineLoggerFactory(string logDirectory, string channel)
    {
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, $"{channel}.log");
        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddZLoggerConsole();
            builder.AddZLoggerFile(logPath);
        });
    }

    public static ILoggerFactory CreateConsoleLoggerFactory()
        => LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddZLoggerStream(System.Console.OpenStandardError());
        });
}
