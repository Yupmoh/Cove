namespace Cove.Dictation;

public sealed class PartialTranscriptTracker
{
    public const double ClosedSilenceSeconds = 0.6;
    public const double OpenPreviewCapSeconds = 15;
    public const double SnapshotWindowSeconds = 25;
    private const double SilentSkipKeepSeconds = 1;

    private readonly ISpeechTrimmer _trimmer;
    private readonly ITranscriber _transcriber;
    private int _offset;
    private string _committed = "";
    private string _lastPartial = "";

    public PartialTranscriptTracker(ISpeechTrimmer trimmer, ITranscriber transcriber)
    {
        _trimmer = trimmer;
        _transcriber = transcriber;
    }

    public string? Advance(AudioSnapshot snapshot)
    {
        var samples = snapshot.Samples;
        var end = snapshot.Offset + samples.Length;
        if (end <= _offset)
            return null;
        if (_offset < snapshot.Offset)
            _offset = snapshot.Offset;
        var tail = samples[(_offset - snapshot.Offset)..];
        var spans = _trimmer.Analyze(tail);
        if (spans.Length == 0)
        {
            var keep = (int)(SilentSkipKeepSeconds * DictationService.SampleRate);
            if (tail.Length > 2 * keep)
                _offset = end - keep;
            return null;
        }

        var cutoff = tail.Length - (int)(ClosedSilenceSeconds * DictationService.SampleRate);
        var advance = 0;
        var openSamples = 0;
        foreach (var span in spans)
        {
            if (span.End <= cutoff)
            {
                var text = _transcriber.Transcribe(tail[span.Start..span.End]).Trim();
                if (text.Length > 0)
                    _committed = Join(_committed, text);
                advance = Math.Max(advance, span.End);
            }
            else
            {
                openSamples += span.End - span.Start;
            }
        }

        var openText = "";
        if (openSamples > 0)
        {
            var open = new float[openSamples];
            var position = 0;
            foreach (var span in spans)
            {
                if (span.End <= cutoff)
                    continue;
                Array.Copy(tail, span.Start, open, position, span.End - span.Start);
                position += span.End - span.Start;
            }
            var cap = (int)(OpenPreviewCapSeconds * DictationService.SampleRate);
            if (open.Length > cap)
                open = open[^cap..];
            openText = _transcriber.Transcribe(open).Trim();
        }

        if (advance > 0)
            _offset += advance;

        var partial = Join(_committed, openText);
        if (partial.Length == 0 || partial == _lastPartial)
            return null;
        _lastPartial = partial;
        return partial;
    }

    private static string Join(string left, string right) =>
        left.Length == 0 ? right : right.Length == 0 ? left : left + " " + right;
}
