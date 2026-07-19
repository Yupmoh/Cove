using System.Diagnostics;
using System.Net;
using Cove.Dictation;
using Xunit;

namespace Cove.Dictation.Tests;

public sealed class DictationModelManagerTests
{
    private sealed class StubHandler(HttpContent content) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Stream CreateContentReadStream(CancellationToken cancellationToken) =>
            new MemoryStream(bytes, writable: false);
    }

    private sealed class ReadTrackingStream : MemoryStream
    {
        public int Reads { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Reads++;
            return base.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            Reads++;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class OversizedDeclaredLengthHandler(string expectedUrl, long maxBytes)
        : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public ReadTrackingStream Stream { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            Assert.Equal(expectedUrl, request.RequestUri?.AbsoluteUri);
            var content = new StreamContent(Stream);
            content.Headers.ContentLength = checked(maxBytes + 1);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class FakeArchiveProcess : IArchiveProcess
    {
        private readonly TaskCompletionSource _exit =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource WaitEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool KillCalled { get; private set; }
        public bool EntireTree { get; private set; }
        public bool ThrowOnKill { get; init; }
        public int ExitCode => 0;

        public Task<string> ReadStandardErrorAsync() => Task.FromResult("");

        public async Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            WaitEntered.TrySetResult();
            await _exit.Task.WaitAsync(cancellationToken);
        }

        public void Kill(bool entireProcessTree)
        {
            KillCalled = true;
            EntireTree = entireProcessTree;
            if (ThrowOnKill)
                throw new InvalidOperationException("kill failed");
            _exit.TrySetResult();
        }

        public void Dispose() => _exit.TrySetResult();
    }

    private sealed class FakeArchiveProcessFactory(FakeArchiveProcess process) : IArchiveProcessFactory
    {
        public IArchiveProcess Start(ProcessStartInfo startInfo)
        {
            var destination = startInfo.ArgumentList[^1];
            File.WriteAllText(Path.Combine(destination, "partial"), "partial");
            return process;
        }
    }

    [Fact]
    public void ModelDirAbsent_TryGetReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-dictate-" + Guid.NewGuid().ToString("N")[..8]);
        var mgr = new DictationModelManager(root);
        Assert.Null(mgr.TryGetModelDir());
    }

    [Fact]
    public void CompleteModelDir_TryGetReturnsIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-dictate-" + Guid.NewGuid().ToString("N")[..8]);
        var dir = Path.Combine(root, DictationModelManager.ModelDirName);
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var f in DictationModelManager.RequiredFiles)
                File.WriteAllText(Path.Combine(dir, f), "x");
            File.WriteAllText(Path.Combine(root, DictationModelManager.VadFileName), "x");
            var mgr = new DictationModelManager(root);
            Assert.Equal(dir, mgr.TryGetModelDir());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IncompleteModelDir_TryGetReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-dictate-" + Guid.NewGuid().ToString("N")[..8]);
        var dir = Path.Combine(root, DictationModelManager.ModelDirName);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "tokens.txt"), "x");
            var mgr = new DictationModelManager(root);
            Assert.Null(mgr.TryGetModelDir());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChecksumMismatch_DeletesArchiveAndThrows()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-dictate-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            var archive = Path.Combine(root, "bogus.tar.bz2");
            await File.WriteAllTextAsync(archive, "not a real archive");
            await Assert.ThrowsAsync<DictationException>(
                () => DictationModelManager.VerifyChecksumAsync(archive, new string('0', 64), CancellationToken.None));
            Assert.False(File.Exists(archive));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Download_RejectsOversizedDeclaredLengthBeforeReading()
    {
        var root = CreateRoot();
        var destination = Path.Combine(root, "artifact.partial");
        var stream = new ReadTrackingStream();
        var content = new StreamContent(stream);
        content.Headers.ContentLength = 11;
        var handler = new StubHandler(content);
        var manager = CreateManager(root, handler);
        try
        {
            var exception = await Assert.ThrowsAsync<DictationException>(
                () => manager.DownloadArtifactAsync("https://example.invalid/artifact", destination, 10, CancellationToken.None));

            Assert.Contains("exceeds", exception.Message, StringComparison.Ordinal);
            Assert.Equal(1, handler.Requests);
            Assert.Equal(0, stream.Reads);
            Assert.False(File.Exists(destination));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Download_RejectsUnknownLengthOverflowBeforeWritingPastCap()
    {
        var root = CreateRoot();
        var destination = Path.Combine(root, "artifact.partial");
        var handler = new StubHandler(new UnknownLengthContent(new byte[11]));
        var manager = CreateManager(root, handler);
        try
        {
            var exception = await Assert.ThrowsAsync<DictationException>(
                () => manager.DownloadArtifactAsync("https://example.invalid/artifact", destination, 10, CancellationToken.None));

            Assert.Contains("exceeds", exception.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(destination));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModel_AppliesVadCapToVadUrl()
    {
        var root = CreateRoot();
        var handler = new OversizedDeclaredLengthHandler(
            DictationModelManager.VadUrl,
            DictationModelManager.VadMaxBytes);
        var manager = CreateManager(root, handler);
        try
        {
            var exception = await Assert.ThrowsAsync<DictationException>(
                () => manager.EnsureModelAsync(null, CancellationToken.None)
                    .WaitAsync(TimeSpan.FromSeconds(2)));

            Assert.Equal(
                $"artifact size {DictationModelManager.VadMaxBytes + 1} exceeds byte cap {DictationModelManager.VadMaxBytes}",
                exception.Message);
            Assert.Equal(1, handler.Requests);
            Assert.Equal(0, handler.Stream.Reads);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModel_AppliesModelArchiveCapToModelUrl()
    {
        var root = CreateRoot();
        File.WriteAllText(Path.Combine(root, DictationModelManager.VadFileName), "present");
        var handler = new OversizedDeclaredLengthHandler(
            DictationModelManager.ModelUrl,
            DictationModelManager.ModelArchiveMaxBytes);
        var manager = CreateManager(root, handler);
        try
        {
            var exception = await Assert.ThrowsAsync<DictationException>(
                () => manager.EnsureModelAsync(null, CancellationToken.None)
                    .WaitAsync(TimeSpan.FromSeconds(2)));

            Assert.Equal(
                $"artifact size {DictationModelManager.ModelArchiveMaxBytes + 1} exceeds byte cap {DictationModelManager.ModelArchiveMaxBytes}",
                exception.Message);
            Assert.Equal(1, handler.Requests);
            Assert.Equal(0, handler.Stream.Reads);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractionCancellation_KillsTreeAndRemovesPartialExtraction()
    {
        var root = CreateRoot();
        var archive = Path.Combine(root, "model.tar.bz2");
        await File.WriteAllTextAsync(archive, "archive");
        var process = new FakeArchiveProcess();
        var manager = CreateManager(
            root,
            new StubHandler(new ByteArrayContent([])),
            new FakeArchiveProcessFactory(process));
        using var cancellation = new CancellationTokenSource();
        try
        {
            var extraction = manager.ExtractModelArchiveAsync(archive, cancellation.Token);
            await process.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => extraction.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.True(process.KillCalled);
            Assert.True(process.EntireTree);
            Assert.Empty(Directory.GetDirectories(root, ".extract-*"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractionTimeout_KillsTreeAndRemovesPartialExtraction()
    {
        var root = CreateRoot();
        var archive = Path.Combine(root, "model.tar.bz2");
        await File.WriteAllTextAsync(archive, "archive");
        var process = new FakeArchiveProcess();
        var manager = CreateManager(
            root,
            new StubHandler(new ByteArrayContent([])),
            new FakeArchiveProcessFactory(process),
            extractionTimeout: TimeSpan.FromMilliseconds(50));
        try
        {
            var extraction = manager.ExtractModelArchiveAsync(archive, CancellationToken.None);
            await process.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

            await Assert.ThrowsAsync<TimeoutException>(
                () => extraction.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.True(process.KillCalled);
            Assert.True(process.EntireTree);
            Assert.Empty(Directory.GetDirectories(root, ".extract-*"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractionCancellation_PreservesKillAndExitCleanupFailures()
    {
        var root = CreateRoot();
        var archive = Path.Combine(root, "model.tar.bz2");
        await File.WriteAllTextAsync(archive, "archive");
        var process = new FakeArchiveProcess { ThrowOnKill = true };
        var manager = CreateManager(
            root,
            new StubHandler(new ByteArrayContent([])),
            new FakeArchiveProcessFactory(process),
            cleanupTimeout: TimeSpan.FromMilliseconds(50));
        using var cancellation = new CancellationTokenSource();
        try
        {
            var extraction = manager.ExtractModelArchiveAsync(archive, cancellation.Token);
            await process.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();

            var aggregate = await Assert.ThrowsAsync<AggregateException>(
                () => extraction.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.IsAssignableFrom<OperationCanceledException>(aggregate.InnerExceptions[0]);
            Assert.Contains(aggregate.InnerExceptions, ex => ex.Message == "kill failed");
            Assert.Contains(aggregate.InnerExceptions, ex => ex is TimeoutException);
            Assert.Empty(Directory.GetDirectories(root, ".extract-*"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-dictate-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        return root;
    }

    private static DictationModelManager CreateManager(
        string root,
        HttpMessageHandler handler,
        IArchiveProcessFactory? processFactory = null,
        TimeSpan? extractionTimeout = null,
        TimeSpan? cleanupTimeout = null) =>
        new(
            root,
            null,
            handler,
            processFactory ?? new FakeArchiveProcessFactory(new FakeArchiveProcess()),
            extractionTimeout ?? TimeSpan.FromMinutes(10),
            cleanupTimeout ?? TimeSpan.FromSeconds(2));
}
