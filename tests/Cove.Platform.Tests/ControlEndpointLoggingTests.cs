using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cove.Platform.Ipc;
using Cove.Testing;
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

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public async Task Bind_logs_socket_path_and_bound_address()
    {
        string dir = TestDirectory.Create("cove-ipc-");
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
            TestFile.Delete(socketPath);
            TestDirectory.Delete(dir);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void Probe_logs_socket_path_on_client_side()
    {
        string dir = TestDirectory.Create("cove-ipc-");
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
            TestDirectory.Delete(dir);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public async Task Socket_path_endpoint_exchanges_data()
    {
        string dir = Path.Combine("/tmp", "cove-ipc-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        string socketPath = Path.Combine(dir, "dev.sock");
        var endpoint = ControlEndpointFactory.FromSocketPath(socketPath);

        try
        {
            await using var listener = endpoint.Bind();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task<Stream> accept = listener.AcceptAsync(cancellation.Token).AsTask();
            await using var client = await endpoint.ConnectAsync(1_000, cancellation.Token);
            await using var server = await accept;

            await client.WriteAsync("ping"u8.ToArray(), cancellation.Token);
            var buffer = new byte[4];
            await server.ReadExactlyAsync(buffer, cancellation.Token);

            Assert.Equal("ping"u8.ToArray(), buffer);
        }
        finally
        {
            TestFile.Delete(socketPath);
            TestDirectory.Delete(dir);
        }
    }
}
