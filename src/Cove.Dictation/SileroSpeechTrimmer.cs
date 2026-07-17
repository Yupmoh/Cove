using SherpaOnnx;

namespace Cove.Dictation;

public sealed class SileroSpeechTrimmer : ISpeechTrimmer, IDisposable
{
    private readonly VoiceActivityDetector _vad;
    private readonly int _windowSize;
    private readonly object _sync = new();

    public SileroSpeechTrimmer(string vadModelPath)
    {
        var config = new VadModelConfig();
        config.SileroVad.Model = vadModelPath;
        config.SileroVad.Threshold = 0.5f;
        config.SileroVad.MinSilenceDuration = 0.25f;
        config.SileroVad.MinSpeechDuration = 0.1f;
        config.SileroVad.WindowSize = 512;
        config.SampleRate = DictationService.SampleRate;
        _windowSize = config.SileroVad.WindowSize;
        _vad = new VoiceActivityDetector(config, bufferSizeInSeconds: (float)DictationService.MaxClipSeconds);
    }

    public float[] Trim(float[] samples)
    {
        lock (_sync)
        {
            _vad.Reset();
            var spans = new List<(int Start, int End)>();
            for (var offset = 0; offset < samples.Length; offset += _windowSize)
            {
                var count = Math.Min(_windowSize, samples.Length - offset);
                var window = new float[count];
                Array.Copy(samples, offset, window, 0, count);
                _vad.AcceptWaveform(window);
            }
            _vad.Flush();
            while (!_vad.IsEmpty())
            {
                var segment = _vad.Front();
                spans.Add((segment.Start, segment.Start + segment.Samples.Length));
                _vad.Pop();
            }
            if (spans.Count == 0)
                return [];

            var pad = DictationService.SampleRate / 4;
            var merged = new List<(int Start, int End)>();
            foreach (var span in spans)
            {
                var start = Math.Max(0, span.Start - pad);
                var end = Math.Min(samples.Length, span.End + pad);
                if (merged.Count > 0 && start <= merged[^1].End)
                    merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, end));
                else
                    merged.Add((start, end));
            }

            var total = 0;
            foreach (var (start, end) in merged)
                total += end - start;
            var result = new float[total];
            var position = 0;
            foreach (var (start, end) in merged)
            {
                Array.Copy(samples, start, result, position, end - start);
                position += end - start;
            }
            return result;
        }
    }

    public void Dispose() => _vad.Dispose();
}
