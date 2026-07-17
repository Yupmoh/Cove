using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Cove.Dictation;

public sealed class DictationModelManager
{
    public const string ModelDirName = "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8";
    public const string VadFileName = "silero_vad.onnx";

    public const string ModelUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2";
    public const string VadUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/silero_vad.onnx";

    public const string ModelSha256 = "5793d0fd397c5778d2cf2126994d58e9d56b1be7c04d13c7a15bb1b4eafb16bf";
    public const string VadSha256 = "9e2449e1087496d8d4caba907f23e0bd3f78d91fa552479bb9c23ac09cbb1fd6";

    public static readonly string[] RequiredFiles =
    [
        "encoder.int8.onnx", "decoder.int8.onnx", "joiner.int8.onnx", "tokens.txt",
    ];

    private readonly string _modelsRoot;
    private readonly ILogger? _logger;

    public DictationModelManager(string modelsRoot, ILogger? logger = null)
    {
        _modelsRoot = modelsRoot;
        _logger = logger;
    }

    public string VadPath => Path.Combine(_modelsRoot, VadFileName);

    public string? TryGetModelDir()
    {
        var dir = Path.Combine(_modelsRoot, ModelDirName);
        if (!Directory.Exists(dir))
            return null;
        foreach (var file in RequiredFiles)
            if (!File.Exists(Path.Combine(dir, file)))
                return null;
        if (!File.Exists(VadPath))
            return null;
        return dir;
    }

    public async Task<string> EnsureModelAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (TryGetModelDir() is { } existing)
            return existing;
        Directory.CreateDirectory(_modelsRoot);

        if (!File.Exists(VadPath))
        {
            var vadPartial = VadPath + ".partial";
            await DownloadAsync(VadUrl, vadPartial, null, cancellationToken).ConfigureAwait(false);
            await VerifyChecksumAsync(vadPartial, VadSha256, cancellationToken).ConfigureAwait(false);
            File.Move(vadPartial, VadPath, overwrite: true);
        }

        var archive = Path.Combine(_modelsRoot, ModelDirName + ".tar.bz2");
        var archivePartial = archive + ".partial";
        await DownloadAsync(ModelUrl, archivePartial, progress, cancellationToken).ConfigureAwait(false);
        await VerifyChecksumAsync(archivePartial, ModelSha256, cancellationToken).ConfigureAwait(false);
        File.Move(archivePartial, archive, overwrite: true);

        var extractTemp = Path.Combine(_modelsRoot, ".extract-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(extractTemp);
        try
        {
            await ExtractAsync(archive, extractTemp, cancellationToken).ConfigureAwait(false);
            var extracted = Path.Combine(extractTemp, ModelDirName);
            if (!Directory.Exists(extracted))
                throw new DictationException($"archive did not contain expected dir '{ModelDirName}'");
            var final = Path.Combine(_modelsRoot, ModelDirName);
            if (Directory.Exists(final))
                Directory.Delete(final, recursive: true);
            Directory.Move(extracted, final);
        }
        finally
        {
            try { Directory.Delete(extractTemp, recursive: true); } catch (IOException) { }
            try { File.Delete(archive); } catch (IOException) { }
        }

        var dir = TryGetModelDir() ?? throw new DictationException("model extraction incomplete: required files missing");
        _logger?.DictationModelReady(dir);
        return dir;
    }

    private async Task DownloadAsync(string url, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        _logger?.DictationModelDownloadStarted(url);
        using var http = new HttpClient();
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? -1;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = File.Create(destination);
        var buffer = new byte[1 << 16];
        long written = 0;
        int n;
        while ((n = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, n), cancellationToken).ConfigureAwait(false);
            written += n;
            if (total > 0)
                progress?.Report((double)written / total);
        }
    }

    public static async Task VerifyChecksumAsync(string path, string expectedSha256, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        if (!hash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(path);
            throw new DictationException($"model checksum mismatch: expected {expectedSha256}, got {hash}");
        }
    }

    private static async Task ExtractAsync(string archive, string destination, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo("tar")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("xjf");
        psi.ArgumentList.Add(archive);
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(destination);
        using var process = Process.Start(psi) ?? throw new DictationException("failed to start tar");
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new DictationException($"tar extraction failed: {await stderr.ConfigureAwait(false)}");
    }
}
