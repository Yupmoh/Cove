namespace Cove.Dictation;

public sealed class DictationException : Exception
{
    public DictationException(string message) : base(message) { }
    public DictationException(string message, Exception inner) : base(message, inner) { }
}

public enum DictationState { Idle, Recording, Transcribing }

public sealed record DictationResult(string Text, double AudioSeconds, long TranscribeMs);

public readonly record struct SpeechSpan(int Start, int End);

public readonly record struct AudioSnapshot(float[] Samples, int Offset);

public interface IAudioRecorder
{
    void Start();
    float[] Stop();
    AudioSnapshot Snapshot(double maxSeconds);
}

public interface ISpeechTrimmer
{
    float[] Trim(float[] samples);
    SpeechSpan[] Analyze(float[] samples);
}

public interface ITranscriber
{
    string Transcribe(float[] samples);
}
