using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cove.Platform.Ipc;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class ControlEndpointLoggingTests
{
    private sealed record Entry(LogLevel Level, string Message);

    private sealed class CapturingLogger : ILogger
    {
        public List<Entry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new Entry(logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    [Fact]
    public async Task Bind_logs_socket_path_and_bound_address()
    {
        if (OperatingSystem.IsWindows())
            return;

        string dir = Path.Combine(Path.GetTempPath(), "cove-ipc-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        string socketPath = Path.Combine(dir, "stable.sock");
        var logger = new CapturingLogger();

        var endpoint = ControlEndpointFactory.FromSocketPath(socketPath, logger);
        try
        {
            var listener = endpoint.Bind();
            await listener.DisposeAsync();

            Assert.Contains(logger.Entries, e => e.Message.Contains(socketPath) && e.Message.Contains("bind begin"));
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("bound") && e.Message.Contains(socketPath));
        }
        finally
        {
            try { File.Delete(socketPath); } catch { }
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void Probe_logs_socket_path_on_client_side()
    {
        if (OperatingSystem.IsWindows())
            return;

        string dir = Path.Combine(Path.GetTempPath(), "cove-ipc-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        string socketPath = Path.Combine(dir, "stable.sock");
        var logger = new CapturingLogger();

        var endpoint = ControlEndpointFactory.FromSocketPath(socketPath, logger);
        try
        {
            bool reachable = endpoint.TryProbe(200);

            Assert.False(reachable);
            Assert.Contains(logger.Entries, e => e.Message.Contains("probe begin") && e.Message.Contains(socketPath));
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("unreachable") && e.Message.Contains(socketPath));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
