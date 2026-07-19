using System.Diagnostics;
using Cove.Dictation;
using Cove.Testing;
using Xunit;

namespace Cove.Dictation.Tests;

[Collection("Dictation real model")]
public sealed class DictationRealModelTests
{
    private static string ModelsRoot =>
        Environment.GetEnvironmentVariable("COVE_DICTATION_MODEL_ROOT")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cove-dev", "models");

    private static string RequiredModelDir()
    {
        var modelDir = new DictationModelManager(ModelsRoot).TryGetModelDir();
        return TestPrerequisite.RequireDirectory(
            modelDir ?? Path.Combine(ModelsRoot, DictationModelManager.ModelDirName),
            $"Dictation model is missing under {ModelsRoot}.");
    }

    private static async Task<float[]> SpeakAsync(string text)
    {
        TestPrerequisite.RequireExecutable("say");
        TestPrerequisite.RequireExecutable("afconvert");
        var wav = Path.Combine(Path.GetTempPath(), "cove-dictate-say-" + Guid.NewGuid().ToString("N")[..8] + ".wav");
        var aiff = Path.ChangeExtension(wav, ".aiff");
        try
        {
            await RunAsync("say", "-o", aiff, text);
            await RunAsync("afconvert", "-f", "WAVE", "-d", "LEI16@16000", "-c", "1", aiff, wav);
            return ReadWavMono16(wav);
        }
        finally
        {
            TestFile.Delete(aiff);
            TestFile.Delete(wav);
        }
    }

    private static async Task RunAsync(string command, params string[] args)
    {
        var psi = new ProcessStartInfo(command)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in args)
            psi.ArgumentList.Add(argument);
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        int exitCode;
        string output;
        string error;
        try
        {
            exitCode = await TestProcess.WaitForExitAsync(process, TimeSpan.FromSeconds(30));
        }
        finally
        {
            output = await stdout;
            error = await stderr;
        }
        Assert.True(exitCode == 0, $"{command} exited {exitCode}. stdout: {output} stderr: {error}");
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

    [LiveFact(TestOperatingSystem.MacOS)]
    public async Task EnsureModel_DownloadsVerifiesAndExtracts()
    {
        TestPrerequisite.RequireFlag("COVE_DICTATION_ENSURE_MODEL");
        var manager = new DictationModelManager(ModelsRoot);
        var deadline = TimeSpan.FromMinutes(10);
        using var timeout = new CancellationTokenSource(deadline);
        string directory;
        try
        {
            directory = await manager.EnsureModelAsync(null, timeout.Token);
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"dictation model provisioning exceeded {deadline}", exception);
        }
        Assert.True(Directory.Exists(directory));
        foreach (var file in DictationModelManager.RequiredFiles)
            Assert.True(File.Exists(Path.Combine(directory, file)), file);
    }

    [LiveFact(TestOperatingSystem.MacOS)]
    public async Task RealModel_TranscribesSynthesizedSpeech()
    {
        var modelDir = RequiredModelDir();
        var samples = await SpeakAsync("hello world");
        using var trimmer = new SileroSpeechTrimmer(Path.Combine(ModelsRoot, DictationModelManager.VadFileName));
        using var transcriber = new SherpaTranscriber(modelDir);
        var speech = trimmer.Trim(samples);
        Assert.NotEmpty(speech);
        var text = transcriber.Transcribe(speech).ToLowerInvariant();
        Assert.Contains("hello", text);
        Assert.Contains("world", text);
    }

    [LiveFact(TestOperatingSystem.MacOS)]
    public async Task RealModel_MidSentencePause_NoPhantomText()
    {
        var modelDir = RequiredModelDir();
        var first = await SpeakAsync("the quick brown fox");
        var second = await SpeakAsync("jumps over the lazy dog");
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
            .Select(word => word.Trim('.', ',', '!', '?'))
            .Where(word => word.Length > 0);
        foreach (var word in words)
            Assert.Contains(word, allowed);
    }

    [LiveFact(TestOperatingSystem.MacOS)]
    public async Task RealModel_SecondClipAfterSpeech_SilenceYieldsNothing()
    {
        _ = RequiredModelDir();
        using var trimmer = new SileroSpeechTrimmer(Path.Combine(ModelsRoot, DictationModelManager.VadFileName));
        var speech = trimmer.Trim(await SpeakAsync("testing one two three"));
        Assert.NotEmpty(speech);
        var silence = trimmer.Trim(new float[DictationService.SampleRate * 2]);
        Assert.Empty(silence);
    }

    [LiveFact(TestOperatingSystem.MacOS)]
    public async Task RealModel_PartialTracker_ProducesProgressivePreview()
    {
        var modelDir = RequiredModelDir();
        var first = await SpeakAsync("hello world");
        var pause = new float[DictationService.SampleRate];
        var firstClip = new float[first.Length + pause.Length];
        first.CopyTo(firstClip, 0);

        using var trimmer = new SileroSpeechTrimmer(Path.Combine(ModelsRoot, DictationModelManager.VadFileName));
        using var transcriber = new SherpaTranscriber(modelDir);
        var tracker = new PartialTranscriptTracker(trimmer, transcriber);

        var partial = tracker.Advance(new AudioSnapshot(firstClip, 0));
        Assert.NotNull(partial);
        Assert.Contains("hello", partial!.ToLowerInvariant());

        var second = await SpeakAsync("testing dictation");
        var grown = new float[firstClip.Length + second.Length];
        firstClip.CopyTo(grown, 0);
        second.CopyTo(grown, firstClip.Length);

        var partial2 = tracker.Advance(new AudioSnapshot(grown, 0));
        Assert.NotNull(partial2);
        Assert.Contains("hello", partial2!.ToLowerInvariant());
        Assert.Contains("testing", partial2!.ToLowerInvariant());
    }
}

[CollectionDefinition("Dictation real model", DisableParallelization = true)]
public sealed class DictationRealModelCollection
{
}
