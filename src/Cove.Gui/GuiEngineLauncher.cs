using System.Diagnostics;
using Cove.Platform;
using Cove.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Gui;

public static class GuiEngineLauncher
{
    public static ILogger Logger { get; set; } = NullLogger.Instance;

    public static async Task<Stream> ConnectOrSpawnAsync(string channel, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        return await ConnectOrSpawnAsync(
            channel,
            SystemEngineProcessLauncher.Instance,
            EndpointDialer.DialAsync,
            static (delay, cancellationToken) =>
                Task.Delay(delay, cancellationToken),
            () => stopwatch.ElapsedMilliseconds,
            ct).ConfigureAwait(false);
    }

    internal static async Task<Stream> ConnectOrSpawnAsync(
        string channel,
        IEngineProcessLauncher processLauncher,
        Func<string, CancellationToken, Task<Stream>> dial,
        Func<TimeSpan, CancellationToken, Task> delay,
        Func<long> elapsedMilliseconds,
        CancellationToken ct)
    {
        var target = GuiLogging.EndpointFor(channel);
        Logger.LauncherDialAttempt(channel, target);
        Stream? s = await TryDialAsync(
                channel,
                target,
                dial,
                ct)
            .ConfigureAwait(false);
        if (s is not null)
            return s;

        Logger.LauncherSpawn(channel);
        processLauncher.Launch(channel);

        var startedAt = elapsedMilliseconds();
        while (elapsedMilliseconds() - startedAt
               < ProtocolConstants.ReadinessTimeoutMs)
        {
            await delay(
                    TimeSpan.FromMilliseconds(
                        ProtocolConstants.SpawnPollMs),
                    ct)
                .ConfigureAwait(false);
            s = await TryDialAsync(
                    channel,
                    target,
                    dial,
                    ct)
                .ConfigureAwait(false);
            if (s is not null)
                return s;
        }
        Logger.LauncherSpawnTimeout(
            channel,
            target,
            elapsedMilliseconds() - startedAt);
        throw new InvalidOperationException($"cove engine did not become connectable on channel {channel}");
    }

    private static async Task<Stream?> TryDialAsync(
        string channel,
        string target,
        Func<string, CancellationToken, Task<Stream>> dial,
        CancellationToken ct)
    {
        try
        {
            return await dial(channel, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LauncherDialFailed(channel, target, ex.Message);
            return null;
        }
    }
}
