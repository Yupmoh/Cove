using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Bays;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Cove.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BayCwdSpawnTests
{
    private static Task<ControlResponse?> Route(
        NookRegistry nooks,
        BayManager bays,
        JsonElement parameters) =>
        EngineCommandRouter.RouteAsync(
            new ControlRequest("1", "cove://commands/nook.spawn", parameters),
            nooks,
            null,
            bays);

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Spawn_NoExplicitCwd_DefaultsToActiveBayDir()
    {
        var directory = NewDirectory();
        try
        {
            using var registry = new NookRegistry(
                PtyHostFactory.Create(NullLogger.Instance),
                NullLogger.Instance);
            await using var bays = new BayManager();
            await bays.CreateBayAsync("w", directory);
            var parameters = JsonSerializer.SerializeToElement(
                new SpawnParams("/bin/sh", ["-c", "sleep 5"], null, null, 40, 10),
                CoveJsonContext.Default.SpawnParams);

            var response = await Route(registry, bays, parameters);

            Assert.True(response!.Ok);
            var nookId = response.Data!.Value.GetProperty("nookId").GetString()!;
            var descriptor = registry.Descriptors().Single(item => item.NookId == nookId);
            Assert.Equal(directory, descriptor.Cwd);
        }
        finally
        {
            TestDirectory.Delete(directory);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Spawn_ExplicitCwd_OverridesBayDir()
    {
        var directory = NewDirectory();
        var explicitCwd = NewDirectory();
        try
        {
            using var registry = new NookRegistry(
                PtyHostFactory.Create(NullLogger.Instance),
                NullLogger.Instance);
            await using var bays = new BayManager();
            await bays.CreateBayAsync("w", directory);
            var parameters = JsonSerializer.SerializeToElement(
                new SpawnParams("/bin/sh", ["-c", "sleep 5"], explicitCwd, null, 40, 10),
                CoveJsonContext.Default.SpawnParams);

            var response = await Route(registry, bays, parameters);

            Assert.True(response!.Ok);
            var nookId = response.Data!.Value.GetProperty("nookId").GetString()!;
            var descriptor = registry.Descriptors().Single(item => item.NookId == nookId);
            Assert.Equal(explicitCwd, descriptor.Cwd);
        }
        finally
        {
            TestDirectory.Delete(directory);
            TestDirectory.Delete(explicitCwd);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task Spawn_DeletedActiveBayDirectoryFallsBackToHome()
    {
        var staleBay = NewDirectory();
        var home = NewDirectory();
        try
        {
            await using var bays = new BayManager();
            await bays.CreateBayAsync("stale", staleBay);
            Directory.Delete(staleBay);
            var logger = new RecordingLogger();
            using var registry = new NookRegistry(
                PtyHostFactory.Create(logger),
                logger,
                homeDirectory: home);
            const string shellCommand = "printf COVE_CWD_OK; sleep 1";
            var parameters = JsonSerializer.SerializeToElement(
                new SpawnParams(null, [], null, null, 40, 10, ShellCommand: shellCommand),
                CoveJsonContext.Default.SpawnParams);

            var response = await Route(registry, bays, parameters);

            Assert.True(response!.Ok, response.Error?.Message);
            var nookId = response.Data!.Value.GetProperty("nookId").GetString()!;
            var descriptor = registry.Descriptors().Single(item => item.NookId == nookId);
            Assert.Equal(home, descriptor.Cwd);
            Assert.Contains(shellCommand, descriptor.Args);
            Assert.Contains(logger.Messages, message =>
                message.Contains(staleBay, StringComparison.Ordinal)
                && message.Contains("rejected default cwd", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, message =>
                message.Contains(home, StringComparison.Ordinal)
                && message.Contains("falling back to home", StringComparison.Ordinal));
        }
        finally
        {
            TestDirectory.Delete(staleBay);
            TestDirectory.Delete(home);
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    private static string NewDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "cove-bay-cwd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
