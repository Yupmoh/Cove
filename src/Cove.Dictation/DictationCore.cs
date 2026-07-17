namespace Cove.Dictation;

public sealed class DictationException : Exception
{
    public DictationException(string message) : base(message) { }
    public DictationException(string message, Exception inner) : base(message, inner) { }
}

public enum DictationState { Idle, Recording, Transcribing }

public sealed record DictationResult(string Text, double AudioSeconds, long TranscribeMs);

public interface IAudioRecorder
{
    void Start();
    float[] Stop();
}

public interface ISpeechTrimmer
{
    float[] Trim(float[] samples);
}

public interface ITranscriber
{
    string Transcribe(float[] samples);
}
