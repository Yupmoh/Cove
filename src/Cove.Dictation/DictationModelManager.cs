using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("Cove.Dictation.Tests")]

namespace Cove.Dictation;

internal interface IArchiveProcess : IDisposable
{
    int ExitCode { get; }
    Task<string> ReadStandardErrorAsync();
    Task WaitForExitAsync(CancellationToken cancellationToken);
    void Kill(bool entireProcessTree);
}

internal interface IArchiveProcessFactory
{
    IArchiveProcess Start(ProcessStartInfo startInfo);
}

internal sealed class ArchiveProcessFactory : IArchiveProcessFactory
{
    public IArchiveProcess Start(ProcessStartInfo startInfo) =>
        new ArchiveProcess(Process.Start(startInfo) ?? throw new DictationException("failed to start tar"));
}

internal sealed class ArchiveProcess(Process process) : IArchiveProcess
{
    public int ExitCode => process.ExitCode;
    public Task<string> ReadStandardErrorAsync() => process.StandardError.ReadToEndAsync();
    public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);
    public void Kill(bool entireProcessTree) => process.Kill(entireProcessTree);
    public void Dispose() => process.Dispose();
}

public sealed class DictationModelManager
{
    internal const long VadMaxBytes = 16L * 1024 * 1024;
    internal const long ModelArchiveMaxBytes = 2L * 1024 * 1024 * 1024;
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
    private readonly HttpMessageHandler? _httpHandler;
    private readonly IArchiveProcessFactory _processFactory;
    private readonly TimeSpan _extractionTimeout;
    private readonly TimeSpan _processCleanupTimeout;

    public DictationModelManager(string modelsRoot, ILogger? logger = null)
        : this(
            modelsRoot,
            logger,
            null,
            new ArchiveProcessFactory(),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromSeconds(5))
    {
    }

    internal DictationModelManager(
        string modelsRoot,
        ILogger? logger,
        HttpMessageHandler? httpHandler,
        IArchiveProcessFactory processFactory,
        TimeSpan extractionTimeout,
        TimeSpan processCleanupTimeout)
    {
        _modelsRoot = modelsRoot;
        _logger = logger;
        _httpHandler = httpHandler;
        _processFactory = processFactory;
        _extractionTimeout = extractionTimeout;
        _processCleanupTimeout = processCleanupTimeout;
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
            await DownloadAsync(VadUrl, vadPartial, VadMaxBytes, null, cancellationToken).ConfigureAwait(false);
            await VerifyChecksumAsync(vadPartial, VadSha256, cancellationToken).ConfigureAwait(false);
            File.Move(vadPartial, VadPath, overwrite: true);
        }

        var archive = Path.Combine(_modelsRoot, ModelDirName + ".tar.bz2");
        var archivePartial = archive + ".partial";
        await DownloadAsync(ModelUrl, archivePartial, ModelArchiveMaxBytes, progress, cancellationToken).ConfigureAwait(false);
        await VerifyChecksumAsync(archivePartial, ModelSha256, cancellationToken).ConfigureAwait(false);
        File.Move(archivePartial, archive, overwrite: true);

        Exception? extractionFailure = null;
        try
        {
            await ExtractModelArchiveAsync(archive, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            extractionFailure = ex;
        }
        var archiveCleanupFailures = DeleteFile(archive);
        ThrowIfFailed(extractionFailure, archiveCleanupFailures);

        var dir = TryGetModelDir() ?? throw new DictationException("model extraction incomplete: required files missing");
        _logger?.DictationModelReady(dir);
        return dir;
    }

    internal Task DownloadArtifactAsync(
        string url,
        string destination,
        long maxBytes,
        CancellationToken cancellationToken) =>
        DownloadAsync(url, destination, maxBytes, null, cancellationToken);

    private async Task DownloadAsync(
        string url,
        string destination,
        long maxBytes,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        _logger?.DictationModelDownloadStarted(url);
        Exception? primaryFailure = null;
        try
        {
            using var http = _httpHandler is null
                ? new HttpClient()
                : new HttpClient(_httpHandler, disposeHandler: false);
            using var response = await http.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            if (total > maxBytes)
                throw new DictationException($"artifact size {total} exceeds byte cap {maxBytes}");
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var target = File.Create(destination);
            var buffer = new byte[1 << 16];
            long written = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (n > maxBytes - written)
                    throw new DictationException($"artifact size exceeds byte cap {maxBytes}");
                await target.WriteAsync(buffer.AsMemory(0, n), cancellationToken).ConfigureAwait(false);
                written += n;
                if (total > 0)
                    progress?.Report((double)written / total.Value);
            }
        }
        catch (Exception ex)
        {
            primaryFailure = ex;
        }
        if (primaryFailure is not null)
            Throw(primaryFailure, DeleteFile(destination));
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

    internal async Task ExtractModelArchiveAsync(string archive, CancellationToken cancellationToken)
    {
        var extractTemp = Path.Combine(_modelsRoot, ".extract-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(extractTemp);
        Exception? primaryFailure = null;
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
        catch (Exception ex)
        {
            primaryFailure = ex;
        }
        var cleanupFailures = DeleteDirectory(extractTemp);
        ThrowIfFailed(primaryFailure, cleanupFailures);
    }

    private async Task ExtractAsync(string archive, string destination, CancellationToken cancellationToken)
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
        using var process = _processFactory.Start(psi);
        var stderr = process.ReadStandardErrorAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_extractionTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            Exception primary = cancellationToken.IsCancellationRequested
                ? ex
                : new TimeoutException($"tar extraction exceeded {_extractionTimeout}", ex);
            var cleanupFailures = await TerminateProcessAsync(process, stderr).ConfigureAwait(false);
            ThrowIfFailed(primary, cleanupFailures);
        }
        if (process.ExitCode != 0)
        {
            var error = await stderr.WaitAsync(_processCleanupTimeout).ConfigureAwait(false);
            throw new DictationException($"tar extraction failed: {error}");
        }
        await stderr.WaitAsync(_processCleanupTimeout).ConfigureAwait(false);
    }

    private async Task<List<Exception>> TerminateProcessAsync(
        IArchiveProcess process,
        Task<string> stderr)
    {
        var failures = new List<Exception>();
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
        using var exitTimeout = new CancellationTokenSource(_processCleanupTimeout);
        try
        {
            await process.WaitForExitAsync(exitTimeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (exitTimeout.IsCancellationRequested)
        {
            failures.Add(new TimeoutException(
                $"tar did not exit within {_processCleanupTimeout} after termination",
                ex));
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
        try
        {
            await stderr.WaitAsync(_processCleanupTimeout).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
        return failures;
    }

    private static List<Exception> DeleteDirectory(string path)
    {
        var failures = new List<Exception>();
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
        return failures;
    }

    private static List<Exception> DeleteFile(string path)
    {
        var failures = new List<Exception>();
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            failures.Add(ex);
        }
        return failures;
    }

    [DoesNotReturn]
    private static void Throw(Exception primary, List<Exception> cleanupFailures)
    {
        if (cleanupFailures.Count == 0)
            ExceptionDispatchInfo.Capture(primary).Throw();
        throw new AggregateException([primary, .. cleanupFailures]);
    }

    private static void ThrowIfFailed(Exception? primary, List<Exception> cleanupFailures)
    {
        if (primary is not null)
            Throw(primary, cleanupFailures);
        if (cleanupFailures.Count == 1)
            ExceptionDispatchInfo.Capture(cleanupFailures[0]).Throw();
        if (cleanupFailures.Count > 1)
            throw new AggregateException(cleanupFailures);
    }
}
