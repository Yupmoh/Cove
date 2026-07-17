using System.Diagnostics;
using Cove.Dictation;
using Xunit;

namespace Cove.Dictation.Tests;

public sealed class DictationRealModelTests
{
    private static string ModelsRoot =>
        Environment.GetEnvironmentVariable("COVE_DICTATION_MODEL_ROOT")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cove-dev", "models");

    private static string? ResolveModelDir() => new DictationModelManager(ModelsRoot).TryGetModelDir();

    private static float[] Speak(string text)
    {
        var wav = Path.Combine(Path.GetTempPath(), "cove-dictate-say-" + Guid.NewGuid().ToString("N")[..8] + ".wav");
        var aiff = Path.ChangeExtension(wav, ".aiff");
        try
        {
            Run("say", "-o", aiff, text);
            Run("afconvert", "-f", "WAVE", "-d", "LEI16@16000", "-c", "1", aiff, wav);
            return ReadWavMono16(wav);
        }
        finally
        {
            File.Delete(aiff);
            File.Delete(wav);
        }
    }

    private static void Run(string command, params string[] args)
    {
        var psi = new ProcessStartInfo(command) { UseShellExecute = false };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        p.WaitForExit(30000);
        Assert.Equal(0, p.ExitCode);
    }

    private static float[] ReadWavMono16(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var dataOffset = -1;
        for (var i = 12; i < bytes.Length - 8;)
        {
            var id = System.Text.Encoding.ASCII.GetString(bytes, i, 4);
            var size = BitConverter.ToInt32(bytes, i + 4);
            if (id == "data")
            {
                dataOffset = i + 8;
                break;
            }
            i += 8 + size + (size % 2);
        }
        Assert.True(dataOffset > 0, "wav data chunk not found");
        var sampleCount = (bytes.Length - dataOffset) / 2;
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
            samples[i] = BitConverter.ToInt16(bytes, dataOffset + i * 2) / 32768f;
        return samples;
    }

    [Fact]
    public async Task EnsureModel_DownloadsVerifiesAndExtracts()
    {
        if (Environment.GetEnvironmentVariable("COVE_DICTATION_ENSURE_MODEL") != "1") return;
        var mgr = new DictationModelManager(ModelsRoot);
        var dir = await mgr.EnsureModelAsync(null, CancellationToken.None);
        Assert.True(Directory.Exists(dir));
        foreach (var f in DictationModelManager.RequiredFiles)
            Assert.True(File.Exists(Path.Combine(dir, f)), f);
    }

    [Fact]
    public void RealModel_TranscribesSynthesizedSpeech()
    {
        if (!OperatingSystem.IsMacOS()) return;
        if (ResolveModelDir() is not { } modelDir) return;

        var samples = Speak("hello world");
        using var trimmer = new SileroSpeechTrimmer(Path.Combine(ModelsRoot, DictationModelManager.VadFileName));
        using var transcriber = new SherpaTranscriber(modelDir);
        var speech = trimmer.Trim(samples);
        Assert.NotEmpty(speech);
        var text = transcriber.Transcribe(speech).ToLowerInvariant();
        Assert.Contains("hello", text);
        Assert.Contains("world", text);
    }

    [Fact]
    public void RealModel_MidSentencePause_NoPhantomText()
    {
        if (!OperatingSystem.IsMacOS()) return;
        if (ResolveModelDir() is not { } modelDir) return;

        var first = Speak("the quick brown fox");
        var second = Speak("jumps over the lazy dog");
        var pause = new float[DictationService.SampleRate * 3];
        var clip = new float[first.Length + pause.Length + second.Length];
        first.CopyTo(clip, 0);
        second.CopyTo(clip, first.Length + pause.Length);

        using var trimmer = new SileroSpeechTrimmer(Path.Combine(ModelsRoot, DictationModelManager.VadFileName));
        using var transcriber = new SherpaTranscriber(modelDir);
        var text = transcriber.Transcribe(trimmer.Trim(clip)).ToLowerInvariant();

        Assert.Contains("quick brown fox", text);
        Assert.Contains("lazy dog", text);
        var allowed = new HashSet<string> { "the", "quick", "brown", "fox", "jumps", "jumped", "over", "lazy", "dog" };
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('.', ',', '!', '?'))
            .Where(w => w.Length > 0);
        foreach (var word in words)
            Assert.Contains(word, allowed);
    }

    [Fact]
    public void RealModel_SecondClipAfterSpeech_SilenceYieldsNothing()
    {
        if (!OperatingSystem.IsMacOS()) return;
        if (ResolveModelDir() is not { } _) return;

        using var trimmer = new SileroSpeechTrimmer(Path.Combine(ModelsRoot, DictationModelManager.VadFileName));
        var speech = trimmer.Trim(Speak("testing one two three"));
        Assert.NotEmpty(speech);
        var silence = trimmer.Trim(new float[DictationService.SampleRate * 2]);
        Assert.Empty(silence);
    }

    [Fact]
    public void RealModel_PartialTracker_ProducesProgressivePreview()
    {
        if (!OperatingSystem.IsMacOS()) return;
        if (ResolveModelDir() is not { } modelDir) return;

        var first = Speak("hello world");
        var pause = new float[DictationService.SampleRate];
        var firstClip = new float[first.Length + pause.Length];
        first.CopyTo(firstClip, 0);

        using var trimmer = new SileroSpeechTrimmer(Path.Combine(ModelsRoot, DictationModelManager.VadFileName));
        using var transcriber = new SherpaTranscriber(modelDir);
        var tracker = new PartialTranscriptTracker(trimmer, transcriber);

        var partial = tracker.Advance(new AudioSnapshot(firstClip, 0));
        Assert.NotNull(partial);
        Assert.Contains("hello", partial!.ToLowerInvariant());

        var second = Speak("testing dictation");
        var grown = new float[firstClip.Length + second.Length];
        firstClip.CopyTo(grown, 0);
        second.CopyTo(grown, firstClip.Length);

        var partial2 = tracker.Advance(new AudioSnapshot(grown, 0));
        Assert.NotNull(partial2);
        Assert.Contains("hello", partial2!.ToLowerInvariant());
        Assert.Contains("testing", partial2!.ToLowerInvariant());
    }
}
