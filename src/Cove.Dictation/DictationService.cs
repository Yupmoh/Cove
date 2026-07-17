using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Cove.Dictation;

public sealed class DictationService
{
    public const int SampleRate = 16000;
    private const double MinClipSeconds = 0.3;
    public const double MaxClipSeconds = 120;

    private readonly IAudioRecorder _recorder;
    private readonly ISpeechTrimmer _trimmer;
    private readonly ITranscriber _transcriber;
    private readonly ILogger _logger;
    private readonly TimeSpan _partialInterval;
    private readonly object _sync = new();
    private readonly object _decode = new();
    private DictationState _state = DictationState.Idle;
    private CancellationTokenSource? _partialCts;
    private Task _partialLoop = Task.CompletedTask;
    private Task _finalWork = Task.CompletedTask;
    private volatile bool _shutdown;

    public DictationService(IAudioRecorder recorder, ISpeechTrimmer trimmer, ITranscriber transcriber, ILogger logger, TimeSpan? partialInterval = null)
    {
        _recorder = recorder;
        _trimmer = trimmer;
        _transcriber = transcriber;
        _logger = logger;
        _partialInterval = partialInterval ?? TimeSpan.FromSeconds(1);
    }

    public Action<string>? PartialTranscript { get; set; }

    public DictationState State
    {
        get { lock (_sync) return _state; }
    }

    public bool Start()
    {
        lock (_sync)
        {
            if (_state != DictationState.Idle)
                return false;
            _recorder.Start();
            _state = DictationState.Recording;
            if (PartialTranscript is not null)
            {
                _partialCts = new CancellationTokenSource();
                _partialLoop = RunPartialLoop(_partialCts.Token);
            }
        }
        _logger.DictationRecordingStarted("default");
        return true;
    }

    private async Task RunPartialLoop(CancellationToken ct)
    {
        var tracker = new PartialTranscriptTracker(_trimmer, _transcriber);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_partialInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            try
            {
                var snapshot = _recorder.Snapshot(PartialTranscriptTracker.SnapshotWindowSeconds);
                string? partial;
                lock (_decode)
                    partial = tracker.Advance(snapshot);
                if (partial is not null && !ct.IsCancellationRequested)
                    PartialTranscript?.Invoke(partial);
            }
            catch (Exception ex)
            {
                _logger.DictationPartialFailed(ex.Message);
                return;
            }
        }
    }

    public Task<DictationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        float[] clip;
        Task loop;
        lock (_sync)
        {
            if (_state != DictationState.Recording)
                return Task.FromResult(new DictationResult("", 0, 0));
            _partialCts?.Cancel();
            _partialCts = null;
            loop = _partialLoop;
            try
            {
                clip = _recorder.Stop();
            }
            catch
            {
                _state = DictationState.Idle;
                throw;
            }
            _state = DictationState.Transcribing;
        }
        if (cancellationToken.IsCancellationRequested)
        {
            lock (_sync)
                _state = DictationState.Idle;
            return Task.FromResult(new DictationResult("", 0, 0));
        }
        Task<DictationResult> work;
        lock (_sync)
        {
            work = Task.Run(async () =>
            {
                await loop.ConfigureAwait(false);
                return Transcribe(clip);
            });
            _finalWork = work;
        }
        return work;
    }

    public void Shutdown()
    {
        Task loop;
        Task final;
        lock (_sync)
        {
            _shutdown = true;
            _partialCts?.Cancel();
            _partialCts = null;
            loop = _partialLoop;
            final = _finalWork;
        }
        try
        {
            Task.WaitAll(loop, final);
        }
        catch (Exception ex)
        {
            _logger.DictationShutdownDrainFailed(ex.Message);
        }
    }

    private DictationResult Transcribe(float[] clip)
    {
        try
        {
            var maxSamples = (int)(MaxClipSeconds * SampleRate);
            if (clip.Length > maxSamples)
                clip = clip[^maxSamples..];
            var audioSeconds = clip.Length / (double)SampleRate;
            if (audioSeconds < MinClipSeconds)
            {
                _logger.DictationClipSkipped(audioSeconds, "too short");
                return new DictationResult("", audioSeconds, 0);
            }
            float[] speech;
            string text;
            var sw = Stopwatch.StartNew();
            lock (_decode)
            {
                if (_shutdown)
                {
                    _logger.DictationClipSkipped(audioSeconds, "shutdown");
                    return new DictationResult("", audioSeconds, 0);
                }
                speech = _trimmer.Trim(clip);
                if (speech.Length == 0)
                {
                    _logger.DictationClipSkipped(audioSeconds, "no speech");
                    return new DictationResult("", audioSeconds, 0);
                }
                text = _transcriber.Transcribe(speech).Trim();
            }
            _logger.DictationTranscribed(audioSeconds, speech.Length / (double)SampleRate, sw.ElapsedMilliseconds, text.Length);
            return new DictationResult(text, audioSeconds, sw.ElapsedMilliseconds);
        }
        finally
        {
            lock (_sync)
                _state = DictationState.Idle;
        }
    }
}
