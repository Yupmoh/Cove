using System.IO;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Cove.Platform;

public static class CoveLog
{
    public static ILoggerFactory CreateEngineLoggerFactory(string logDirectory, string channel)
        => CreateNamedFileLoggerFactory(logDirectory, channel, LogLevel.Information);

    public static ILoggerFactory CreateNamedFileLoggerFactory(
        string logDirectory,
        string name,
        LogLevel minimumLevel)
    {
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, $"{name}.log");
        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
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
