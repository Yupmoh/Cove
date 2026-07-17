using SherpaOnnx;

namespace Cove.Dictation;

public sealed class SherpaTranscriber : ITranscriber, IDisposable
{
    private readonly OfflineRecognizer _recognizer;

    public SherpaTranscriber(string modelDir, int numThreads = 4)
    {
        var config = new OfflineRecognizerConfig();
        config.ModelConfig.Transducer.Encoder = Path.Combine(modelDir, "encoder.int8.onnx");
        config.ModelConfig.Transducer.Decoder = Path.Combine(modelDir, "decoder.int8.onnx");
        config.ModelConfig.Transducer.Joiner = Path.Combine(modelDir, "joiner.int8.onnx");
        config.ModelConfig.Tokens = Path.Combine(modelDir, "tokens.txt");
        config.ModelConfig.ModelType = "nemo_transducer";
        config.ModelConfig.NumThreads = numThreads;
        config.DecodingMethod = "greedy_search";
        _recognizer = new OfflineRecognizer(config);
    }

    public string Transcribe(float[] samples)
    {
        using var stream = _recognizer.CreateStream();
        stream.AcceptWaveform(DictationService.SampleRate, samples);
        _recognizer.Decode(stream);
        return stream.Result.Text;
    }

    public void Dispose() => _recognizer.Dispose();
}
