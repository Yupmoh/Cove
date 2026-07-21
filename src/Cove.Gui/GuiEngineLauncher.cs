using System.Diagnostics;
using System.Text.Json;
using Cove.Platform;
using Cove.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Gui;

internal readonly record struct EngineProbeResult(
    bool IsCompatible,
    string EngineVersion);

public static class GuiEngineLauncher
{
    private static readonly SemaphoreSlim CompatibilityGate = new(1, 1);

    public static ILogger Logger { get; set; } = NullLogger.Instance;

    public static async Task<Stream> ConnectOrSpawnAsync(
        string channel,
        string expectedVersion,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        await CompatibilityGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await ConnectOrSpawnAsync(
                channel,
                expectedVersion,
                SystemEngineProcessLauncher.Instance,
                EndpointDialer.DialAsync,
                (stream, version, cancellationToken) =>
                    ProbeCompatibilityAsync(
                        stream,
                        channel,
                        version,
                        cancellationToken),
                static (delay, cancellationToken) =>
                    Task.Delay(delay, cancellationToken),
                () => stopwatch.ElapsedMilliseconds,
                ct).ConfigureAwait(false);
        }
        finally
        {
            CompatibilityGate.Release();
        }
    }

    internal static async Task<Stream> ConnectOrSpawnAsync(
        string channel,
        string expectedVersion,
        IEngineProcessLauncher processLauncher,
        Func<string, CancellationToken, Task<Stream>> dial,
        Func<Stream, string, CancellationToken, Task<EngineProbeResult>> probe,
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
        string? predecessorVersion = null;
        if (s is not null)
        {
            var initialProbe = await ProbeAsync(
                    s,
                    channel,
                    target,
                    expectedVersion,
                    probe,
                    ct)
                .ConfigureAwait(false);
            if (initialProbe.IsCompatible)
            {
                await s.DisposeAsync().ConfigureAwait(false);
                s = await TryDialAsync(
                        channel,
                        target,
                        dial,
                        ct)
                    .ConfigureAwait(false);
                if (s is not null)
                    return s;
            }
            else
            {
                predecessorVersion = initialProbe.EngineVersion;
                await s.DisposeAsync().ConfigureAwait(false);
            }
            s = null;
        }

        var startedAt = elapsedMilliseconds();
        if (predecessorVersion is not null)
        {
            Logger.LauncherHandoffRequested(
                channel,
                predecessorVersion,
                expectedVersion);
        }
        Logger.LauncherSpawn(channel);
        processLauncher.Launch(channel);
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
            if (s is null)
                continue;

            var currentProbe = await ProbeAsync(
                    s,
                    channel,
                    target,
                    expectedVersion,
                    probe,
                    ct)
                .ConfigureAwait(false);
            if (currentProbe.IsCompatible)
            {
                await s.DisposeAsync().ConfigureAwait(false);
                s = await TryDialAsync(
                        channel,
                        target,
                        dial,
                        ct)
                    .ConfigureAwait(false);
                if (s is not null)
                    return s;
                continue;
            }
            await s.DisposeAsync().ConfigureAwait(false);
            s = null;
        }
        Logger.LauncherSpawnTimeout(
            channel,
            target,
            elapsedMilliseconds() - startedAt);
        throw new InvalidOperationException($"cove engine did not become connectable on channel {channel}");
    }

    private static async Task<EngineProbeResult> ProbeAsync(
        Stream stream,
        string channel,
        string target,
        string expectedVersion,
        Func<Stream, string, CancellationToken, Task<EngineProbeResult>> probe,
        CancellationToken ct)
    {
        try
        {
            return await probe(stream, expectedVersion, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LauncherCompatibilityFailed(
                channel,
                target,
                expectedVersion,
                ex.Message);
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<EngineProbeResult> ProbeCompatibilityAsync(
        Stream stream,
        string channel,
        string expectedVersion,
        CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(
            ProtocolConstants.ControlRequestTimeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            ct,
            timeout.Token);
        var probeCt = linked.Token;
        var dataDirectory = CoveDataDir.Resolve(
            GuiLogging.ParseChannel(channel));
        string? controlToken = null;
        try
        {
            controlToken = ControlCredential.Read(dataDirectory);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            Logger.ControlTokenMissing(dataDirectory.ControlTokenPath);
        }
        catch (Exception ex)
        {
            Logger.ControlTokenReadFailed(ex.Message);
        }

        using var writeGate = new SemaphoreSlim(1, 1);
        var helloId = Guid.NewGuid().ToString("N");
        var helloParameters = JsonSerializer.SerializeToElement(
            new HelloParams(
                ProtocolConstants.SemanticProtocolVersion,
                "gui",
                expectedVersion,
                channel,
                ControlToken: controlToken),
            CoveJsonContext.Default.HelloParams);
        var hello = new ControlRequest(
            helloId,
            "cove://sys/hello",
            helloParameters);
        await WriteRequestAsync(
                stream,
                writeGate,
                hello,
                1,
                probeCt)
            .ConfigureAwait(false);
        ControlResponse helloResponse;
        try
        {
            helloResponse = await ReadResponseAsync(
                    stream,
                    helloId,
                    probeCt)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (timeout.IsCancellationRequested
                  && !ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"daemon compatibility probe timed out on channel {channel}");
        }
        if (!helloResponse.Ok)
            throw new InvalidOperationException(
                $"hello failed: {helloResponse.Error?.Code ?? "unknown"}");
        var helloResult = helloResponse.Data?.Deserialize(
            CoveJsonContext.Default.HelloResult);
        if (helloResult is null)
            throw new InvalidDataException("hello result is missing");
        if (string.Equals(
                helloResult.EngineVersion,
                expectedVersion,
                StringComparison.Ordinal))
        {
            return new EngineProbeResult(
                true,
                helloResult.EngineVersion);
        }

        Logger.LauncherVersionMismatch(
            channel,
            expectedVersion,
            helloResult.EngineVersion);
        return new EngineProbeResult(
            false,
            helloResult.EngineVersion);
    }

    private static Task WriteRequestAsync(
        Stream stream,
        SemaphoreSlim writeGate,
        ControlRequest request,
        uint sequence,
        CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            request,
            CoveJsonContext.Default.ControlRequest);
        return FrameIo.WriteAsync(
            stream,
            writeGate,
            FrameType.Request,
            0,
            sequence,
            payload,
            ct);
    }

    private static async Task<ControlResponse> ReadResponseAsync(
        Stream stream,
        string requestId,
        CancellationToken ct)
    {
        while (true)
        {
            var frame = await FrameIo.ReadAsync(stream, ct)
                .ConfigureAwait(false);
            if (frame.Type == FrameType.Error)
            {
                var error = JsonSerializer.Deserialize(
                    frame.Payload,
                    CoveJsonContext.Default.ControlErrorFrame);
                throw new InvalidOperationException(
                    $"daemon compatibility probe failed: {error?.Code ?? "unknown"}");
            }
            if (frame.Type != FrameType.Response)
                continue;
            var response = JsonSerializer.Deserialize(
                frame.Payload,
                CoveJsonContext.Default.ControlResponse);
            if (response is not null
                && string.Equals(
                    response.Id,
                    requestId,
                    StringComparison.Ordinal))
            {
                return response;
            }
        }
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
