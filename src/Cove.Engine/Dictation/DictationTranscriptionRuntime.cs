using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Dictation;

public interface IDictationModelProvisioner
{
    string VadPath { get; }

    string? TryGetModelDir();

    Task<string> EnsureModelAsync(
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}

internal sealed class DictationModelProvisioner : IDictationModelProvisioner
{
    private readonly Cove.Dictation.DictationModelManager _manager;

    public DictationModelProvisioner(string modelsRoot, ILogger logger)
    {
        _manager = new Cove.Dictation.DictationModelManager(modelsRoot, logger);
    }

    public string VadPath => _manager.VadPath;

    public string? TryGetModelDir() => _manager.TryGetModelDir();

    public Task<string> EnsureModelAsync(
        IProgress<double>? progress,
        CancellationToken cancellationToken) =>
        _manager.EnsureModelAsync(progress, cancellationToken);
}

public sealed class DictationTranscriptionRuntime : IAsyncDisposable
{
    public const int MaxFinalAudioBytes = 7_680_000;
    public const int MaxPartialAudioBytes =
        (int)(Cove.Dictation.PartialTranscriptTracker.SnapshotWindowSeconds
            * Cove.Dictation.DictationService.SampleRate
            * sizeof(float));

    private readonly IDictationModelProvisioner _provisioner;
    private readonly Func<string, Cove.Dictation.ISpeechTrimmer> _trimmerFactory;
    private readonly Func<string, Cove.Dictation.ITranscriber> _transcriberFactory;
    private readonly Action<string, JsonElement> _publish;
    private readonly ILogger _logger;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _decode = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Cove.Dictation.ISpeechTrimmer? _trimmer;
    private Cove.Dictation.ITranscriber? _transcriber;
    private Session? _session;
    private Task _ensureTask = Task.CompletedTask;
    private Task? _disposeTask;
    private int _finalTranscriptions;

    public DictationTranscriptionRuntime(
        IDictationModelProvisioner provisioner,
        Func<string, Cove.Dictation.ISpeechTrimmer> trimmerFactory,
        Func<string, Cove.Dictation.ITranscriber> transcriberFactory,
        Action<string, JsonElement> publish,
        ILogger logger)
    {
        _provisioner = provisioner;
        _trimmerFactory = trimmerFactory;
        _transcriberFactory = transcriberFactory;
        _publish = publish;
        _logger = logger;
    }

    internal static DictationTranscriptionRuntime CreateNative(
        string modelsRoot,
        Action<string, JsonElement> publish,
        ILogger logger) =>
        new(
            new DictationModelProvisioner(modelsRoot, logger),
            static vadPath => new Cove.Dictation.SileroSpeechTrimmer(vadPath),
            static modelDir => new Cove.Dictation.SherpaTranscriber(modelDir),
            publish,
            logger);

    internal DictationStatusResult Status()
    {
        lock (_sync)
        {
            return new DictationStatusResult(
                ResolveStateLocked(),
                _provisioner.TryGetModelDir() is not null);
        }
    }

    internal bool EnsureModel()
    {
        if (_provisioner.TryGetModelDir() is not null)
            return true;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
            if (_ensureTask.IsCompleted)
            {
                _ensureTask = EnsureModelCoreAsync(_lifetime.Token);
            }
        }
        return false;
    }

    internal string? Begin()
    {
        var modelDir = _provisioner.TryGetModelDir();
        if (modelDir is null)
            return null;

        bool publishIdle;
        string sessionId;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
            EnsureNativeResourcesLocked(modelDir);
            publishIdle = _session?.Recording == true;
            sessionId = Guid.NewGuid().ToString("N");
            _session = new Session(
                sessionId,
                new Cove.Dictation.PartialTranscriptTracker(
                    _trimmer!,
                    _transcriber!));
        }
        if (publishIdle)
            Publish(
                "dictation.state",
                new DictationStateEvent(false),
                CoveJsonContext.Default.DictationStateEvent);
        return sessionId;
    }

    internal bool Started(string sessionId)
    {
        bool publish;
        lock (_sync)
        {
            if (_disposeTask is not null
                || !MatchesLocked(sessionId))
            {
                return false;
            }
            publish = !_session!.Recording;
            _session.Recording = true;
        }
        if (publish)
        {
            Publish(
                "dictation.state",
                new DictationStateEvent(true),
                CoveJsonContext.Default.DictationStateEvent);
        }
        return true;
    }

    internal async Task<bool> PartialAsync(
        string sessionId,
        DictationAudioPayload audio,
        CancellationToken cancellationToken)
    {
        if (!TryValidateAudio(audio, MaxPartialAudioBytes, true, out var sampleCount))
            return false;

        Session session;
        lock (_sync)
        {
            if (_disposeTask is not null
                || !MatchesLocked(sessionId)
                || !_session!.Recording)
            {
                return false;
            }
            session = _session;
        }

        var samples = Decode(audio.Pcm, sampleCount);
        string? text;
        await _decode.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_sync)
            {
                if (_disposeTask is not null
                    || !ReferenceEquals(_session, session)
                    || !session.Recording)
                {
                    return false;
                }
            }
            text = await Task.Run(
                    () => session.Tracker.Advance(
                        new Cove.Dictation.AudioSnapshot(samples, audio.Offset)),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally
        {
            _decode.Release();
        }
        bool current;
        lock (_sync)
        {
            current = _disposeTask is null
                && ReferenceEquals(_session, session)
                && session.Recording;
        }
        if (current && text is not null)
        {
            Publish(
                "dictation.partial",
                new DictationPartialResult(text),
                CoveJsonContext.Default.DictationPartialResult);
        }
        return true;
    }

    internal async Task<DictationTranscriptionResult?> StopAsync(
        string sessionId,
        DictationAudioPayload audio,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_disposeTask is not null
                || !MatchesLocked(sessionId)
                || !_session!.Recording)
            {
                return null;
            }
            _session = null;
            _finalTranscriptions++;
        }
        Publish(
            "dictation.state",
            new DictationStateEvent(false),
            CoveJsonContext.Default.DictationStateEvent);

        try
        {
            if (!TryValidateAudio(audio, MaxFinalAudioBytes, false, out var sampleCount))
                return null;
            var samples = Decode(audio.Pcm, sampleCount);
            await _decode.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var text = await Task.Run(
                        () =>
                        {
                            var speech = _trimmer!.Trim(samples);
                            return speech.Length == 0
                                ? ""
                                : _transcriber!.Transcribe(speech).Trim();
                        },
                        CancellationToken.None)
                    .ConfigureAwait(false);
                return new DictationTranscriptionResult(
                    text,
                    samples.Length
                        / (double)Cove.Dictation.DictationService.SampleRate,
                    stopwatch.ElapsedMilliseconds);
            }
            finally
            {
                _decode.Release();
            }
        }
        finally
        {
            lock (_sync)
                _finalTranscriptions--;
        }
    }

    internal void Cancel(string sessionId)
    {
        bool publish;
        lock (_sync)
        {
            if (_disposeTask is not null || !MatchesLocked(sessionId))
                return;
            _session = null;
            publish = true;
        }
        if (publish)
        {
            Publish(
                "dictation.state",
                new DictationStateEvent(false),
                CoveJsonContext.Default.DictationStateEvent);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            _disposeTask ??= DisposeCoreAsync(_ensureTask);
            _session = null;
            return new ValueTask(_disposeTask);
        }
    }

    private async Task EnsureModelCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var progress = new InlineProgress<double>(
                percent => Publish(
                    "dictation.progress",
                    new DictationProgressEvent(percent),
                    CoveJsonContext.Default.DictationProgressEvent));
            await _provisioner
                .EnsureModelAsync(progress, cancellationToken)
                .ConfigureAwait(false);
            Publish(
                "dictation.model",
                new DictationModelEvent(true),
                CoveJsonContext.Default.DictationModelEvent);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.DictationModelProvisioningCancelled("shutdown");
        }
        catch (Exception exception)
        {
            _logger.DictationModelProvisioningFailed(exception.Message);
            PublishError(exception.Message);
        }
    }

    private async Task DisposeCoreAsync(Task ensureTask)
    {
        _lifetime.Cancel();
        await ensureTask.ConfigureAwait(false);
        await _decode.WaitAsync().ConfigureAwait(false);
        try
        {
            List<Exception>? failures = null;
            TryDisposeResource(_transcriber, ref failures);
            _transcriber = null;
            TryDisposeResource(_trimmer, ref failures);
            _trimmer = null;
            if (failures is { Count: 1 })
                throw failures[0];
            if (failures is not null)
                throw new AggregateException(failures);
        }
        finally
        {
            _decode.Release();
            _decode.Dispose();
            _lifetime.Dispose();
        }
    }

    private void EnsureNativeResourcesLocked(string modelDir)
    {
        if (_trimmer is not null && _transcriber is not null)
            return;

        Cove.Dictation.ISpeechTrimmer? trimmer = null;
        Cove.Dictation.ITranscriber? transcriber = null;
        try
        {
            trimmer = _trimmerFactory(_provisioner.VadPath);
            transcriber = _transcriberFactory(modelDir);
            _trimmer = trimmer;
            _transcriber = transcriber;
        }
        catch
        {
            List<Exception>? failures = null;
            TryDisposeResource(transcriber, ref failures);
            TryDisposeResource(trimmer, ref failures);
            throw;
        }
    }

    private string ResolveStateLocked()
    {
        if (_session?.Recording == true)
            return "recording";
        return _finalTranscriptions > 0 ? "transcribing" : "idle";
    }

    private bool MatchesLocked(string sessionId) =>
        !string.IsNullOrWhiteSpace(sessionId)
        && _session is not null
        && string.Equals(_session.Id, sessionId, StringComparison.Ordinal);

    private static bool TryValidateAudio(
        DictationAudioPayload audio,
        int maxBytes,
        bool validateOffset,
        out int sampleCount)
    {
        sampleCount = 0;
        if (audio.Pcm is null
            || audio.SampleRate != Cove.Dictation.DictationService.SampleRate
            || audio.Pcm.Length > maxBytes
            || audio.Pcm.Length % sizeof(float) != 0)
        {
            return false;
        }
        sampleCount = audio.Pcm.Length / sizeof(float);
        return !validateOffset
            || audio.Offset >= 0
                && audio.Offset <= int.MaxValue - sampleCount;
    }

    private static float[] Decode(byte[] pcm, int sampleCount)
    {
        var samples = new float[sampleCount];
        var source = pcm.AsSpan();
        for (var index = 0; index < samples.Length; index++)
        {
            var bits = BinaryPrimitives.ReadInt32LittleEndian(
                source.Slice(index * sizeof(float), sizeof(float)));
            samples[index] = BitConverter.Int32BitsToSingle(bits);
        }
        return samples;
    }

    private void Publish<T>(
        string channel,
        T payload,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        lock (_sync)
        {
            if (_disposeTask is not null)
                return;
        }
        _publish(channel, JsonSerializer.SerializeToElement(payload, typeInfo));
    }

    private void PublishError(string message)
    {
        lock (_sync)
        {
            if (_disposeTask is not null)
                return;
        }
        _publish(
            "dictation.model",
            JsonSerializer.SerializeToElement(
                new DictationModelErrorEvent(message),
                DictationRuntimeJsonContext.Default.DictationModelErrorEvent));
    }

    private static void DisposeResource<T>(T? resource)
        where T : class
    {
        if (resource is IDisposable disposable)
            disposable.Dispose();
    }

    private static void TryDisposeResource<T>(
        T? resource,
        ref List<Exception>? failures)
        where T : class
    {
        try
        {
            DisposeResource(resource);
        }
        catch (Exception exception)
        {
            failures ??= [];
            failures.Add(exception);
        }
    }

    private sealed class Session(
        string id,
        Cove.Dictation.PartialTranscriptTracker tracker)
    {
        public string Id { get; } = id;
        public Cove.Dictation.PartialTranscriptTracker Tracker { get; } = tracker;
        public bool Recording { get; set; }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

internal sealed record DictationModelErrorEvent(string Error);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DictationModelErrorEvent))]
internal sealed partial class DictationRuntimeJsonContext : JsonSerializerContext;
