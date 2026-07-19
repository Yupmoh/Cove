using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Cove.Dictation;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("Cove.Gui.Tests")]

namespace Cove.Gui;

public sealed class DictationHost : IAsyncDisposable
{
    private const double SnapshotWindowSeconds = 25;
    private readonly ILogger<DictationHost> _log;
    private readonly Func<string, JsonElement?, CancellationToken, Task<ControlResponse>> _request;
    private readonly Func<IAudioRecorder> _recorderFactory;
    private readonly TimeSpan _partialInterval;
    private readonly SemaphoreSlim _operations = new(1, 1);
    private readonly object _disposeSync = new();
    private IAudioRecorder? _recorder;
    private string? _sessionId;
    private CancellationTokenSource? _partialCancellation;
    private Task _partialTask = Task.CompletedTask;
    private Task? _disposeTask;
    private bool _disposed;

    internal DictationHost(
        ILogger<DictationHost> log,
        Func<string, JsonElement?, CancellationToken, Task<ControlResponse>> request)
        : this(
            log,
            request,
            static () => new PortAudioRecorder(),
            TimeSpan.FromSeconds(1))
    {
    }

    internal DictationHost(
        ILogger<DictationHost> log,
        Func<string, JsonElement?, CancellationToken, Task<ControlResponse>> request,
        Func<IAudioRecorder> recorderFactory,
        TimeSpan partialInterval)
    {
        _log = log;
        _request = request;
        _recorderFactory = recorderFactory;
        _partialInterval = partialInterval;
    }

    public Task<string> Status() =>
        ForwardAsync("cove://commands/dictation.status");

    public Task<string> EnsureModel() =>
        ForwardAsync("cove://commands/dictation.ensure-model");

    public async Task<string> StartDictation()
    {
        await _operations.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_sessionId is not null)
                return StartError("busy");

            ControlResponse begin;
            try
            {
                begin = await _request(
                        "cove://commands/dictation.begin",
                        null,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.DictationTransportFailed(
                    "cove://commands/dictation.begin",
                    ex.Message);
                return StartError(ex.Message);
            }

            if (!begin.Ok)
            {
                var error = ResponseError(begin);
                _log.DictationTransportFailed(
                    "cove://commands/dictation.begin",
                    error);
                return StartError(error);
            }

            string sessionId;
            try
            {
                sessionId = ReadSessionId(begin);
            }
            catch (Exception ex)
            {
                _log.DictationTransportFailed(
                    "cove://commands/dictation.begin",
                    ex.Message);
                return StartError(ex.Message);
            }
            if (string.IsNullOrEmpty(sessionId))
            {
                const string error = "engine returned an invalid dictation session";
                _log.DictationTransportFailed(
                    "cove://commands/dictation.begin",
                    error);
                return StartError(error);
            }

            try
            {
                _recorder ??= _recorderFactory();
                _recorder.Start();
            }
            catch (Exception ex)
            {
                _log.DictationCaptureFailed("start", ex.Message);
                await CancelSessionAsync(sessionId).ConfigureAwait(false);
                return StartError(ex.Message);
            }

            try
            {
                await RequestAsync(
                        "cove://commands/dictation.started",
                        SessionParameters(sessionId),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                TryStopRecorder("start-rollback");
                await CancelSessionAsync(sessionId).ConfigureAwait(false);
                return StartError(ex.Message);
            }

            _sessionId = sessionId;
            _partialCancellation = new CancellationTokenSource();
            _partialTask = SendPartialsAsync(
                sessionId,
                _recorder,
                _partialCancellation.Token);
            return """{"ok":true}""";
        }
        finally
        {
            _operations.Release();
        }
    }

    public async ValueTask<string> StopDictation()
    {
        await _operations.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_sessionId is not { } sessionId || _recorder is null)
                return """{"text":""}""";

            await EndPartialLoopAsync().ConfigureAwait(false);
            _sessionId = null;

            float[] samples;
            try
            {
                samples = _recorder.Stop();
            }
            catch (Exception ex)
            {
                _log.DictationCaptureFailed("stop", ex.Message);
                await CancelSessionAsync(sessionId).ConfigureAwait(false);
                throw;
            }

            return await RequestAsync(
                    "cove://commands/dictation.stop",
                    JsonSerializer.SerializeToElement(
                        new DictationStopParams(
                            sessionId,
                            AudioPayload(samples, 0)),
                        CoveJsonContext.Default.DictationStopParams),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally
        {
            _operations.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeSync)
        {
            if (_disposeTask is null)
            {
                _disposed = true;
                _disposeTask = DisposeCoreAsync();
            }
            return new ValueTask(_disposeTask);
        }
    }

    private async Task<string> ForwardAsync(string uri)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await RequestAsync(uri, null, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private async Task<string> RequestAsync(
        string uri,
        JsonElement? parameters,
        CancellationToken cancellationToken)
    {
        ControlResponse response;
        try
        {
            response = await _request(uri, parameters, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.DictationTransportFailed(uri, ex.Message);
            throw;
        }

        if (!response.Ok)
        {
            var error = ResponseError(response);
            _log.DictationTransportFailed(uri, error);
            throw new InvalidOperationException(error);
        }
        return response.Data is { } data ? data.GetRawText() : "{}";
    }

    private async Task SendPartialsAsync(
        string sessionId,
        IAudioRecorder recorder,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await Task.Delay(_partialInterval, cancellationToken)
                    .ConfigureAwait(false);
                var snapshot = recorder.Snapshot(SnapshotWindowSeconds);
                var parameters = JsonSerializer.SerializeToElement(
                    new DictationPartialParams(
                        sessionId,
                        AudioPayload(snapshot.Samples, snapshot.Offset)),
                    CoveJsonContext.Default.DictationPartialParams);
                await RequestAsync(
                        "cove://commands/dictation.partial",
                        parameters,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.DictationPartialFailed(sessionId, ex.Message);
            }
        }
    }

    private async Task EndPartialLoopAsync()
    {
        var cancellation = _partialCancellation;
        _partialCancellation = null;
        cancellation?.Cancel();
        await _partialTask.ConfigureAwait(false);
        _partialTask = Task.CompletedTask;
        cancellation?.Dispose();
    }

    private async Task CancelSessionAsync(string sessionId)
    {
        try
        {
            await RequestAsync(
                    "cove://commands/dictation.cancel",
                    SessionParameters(sessionId),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.DictationCancelFailed(sessionId, ex.Message);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _operations.WaitAsync().ConfigureAwait(false);
        try
        {
            await EndPartialLoopAsync().ConfigureAwait(false);
            if (_sessionId is { } sessionId)
            {
                TryStopRecorder("dispose");
                _sessionId = null;
                await CancelSessionAsync(sessionId).ConfigureAwait(false);
            }

            if (_recorder is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    _log.DictationCaptureFailed("dispose", ex.Message);
                    throw;
                }
            }
            _recorder = null;
        }
        finally
        {
            _operations.Release();
        }
    }

    private void TryStopRecorder(string operation)
    {
        try
        {
            _recorder?.Stop();
        }
        catch (Exception ex)
        {
            _log.DictationCaptureFailed(operation, ex.Message);
        }
    }

    private static JsonElement SessionParameters(string sessionId) =>
        JsonSerializer.SerializeToElement(
            new DictationSessionParams(sessionId),
            CoveJsonContext.Default.DictationSessionParams);

    private static DictationAudioPayload AudioPayload(
        ReadOnlySpan<float> samples,
        int offset)
    {
        var pcm = new byte[checked(samples.Length * sizeof(float))];
        for (var index = 0; index < samples.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                pcm.AsSpan(index * sizeof(float), sizeof(float)),
                BitConverter.SingleToInt32Bits(samples[index]));
        }
        return new DictationAudioPayload(pcm, 16000, offset);
    }

    private static string ReadSessionId(ControlResponse response)
    {
        if (response.Data is not { } data)
            return string.Empty;
        return JsonSerializer.Deserialize(
                data,
                CoveJsonContext.Default.DictationBeginResult)
            ?.SessionId ?? string.Empty;
    }

    private static string ResponseError(ControlResponse response) =>
        response.Error?.Message
        ?? response.Error?.Code
        ?? "engine_error";

    private static string StartError(string error) =>
        JsonSerializer.Serialize(
            new DictationStartErrorDto(false, error),
            DictationJsonContext.Default.DictationStartErrorDto);
}

public sealed record DictationStartErrorDto(bool Ok, string Error);

[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
[System.Text.Json.Serialization.JsonSerializable(typeof(DictationStartErrorDto))]
public sealed partial class DictationJsonContext : System.Text.Json.Serialization.JsonSerializerContext { }
