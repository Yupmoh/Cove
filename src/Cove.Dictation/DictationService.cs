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
    private readonly object _sync = new();
    private DictationState _state = DictationState.Idle;

    public DictationService(IAudioRecorder recorder, ISpeechTrimmer trimmer, ITranscriber transcriber, ILogger logger)
    {
        _recorder = recorder;
        _trimmer = trimmer;
        _transcriber = transcriber;
        _logger = logger;
    }

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
        }
        _logger.DictationRecordingStarted("default");
        return true;
    }

    public Task<DictationResult> StopAsync(CancellationToken cancellationToken = default)
    {
        float[] clip;
        lock (_sync)
        {
            if (_state != DictationState.Recording)
                return Task.FromResult(new DictationResult("", 0, 0));
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
        return Task.Run(() => Transcribe(clip));
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
            var speech = _trimmer.Trim(clip);
            if (speech.Length == 0)
            {
                _logger.DictationClipSkipped(audioSeconds, "no speech");
                return new DictationResult("", audioSeconds, 0);
            }
            var sw = Stopwatch.StartNew();
            var text = _transcriber.Transcribe(speech).Trim();
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
