using Cove.Dictation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Dictation.Tests;

public sealed class DictationServiceTests
{
    private sealed class FakeRecorder : IAudioRecorder
    {
        public float[] Clip = [];
        public bool Started;
        public bool ThrowOnStart;
        public int SnapshotCalls;
        public TaskCompletionSource SnapshotObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Start()
        {
            if (ThrowOnStart)
                throw new DictationException("microphone unavailable");
            Started = true;
        }

        public float[] Stop()
        {
            Started = false;
            return Clip;
        }

        public AudioSnapshot Snapshot(double maxSeconds)
        {
            Interlocked.Increment(ref SnapshotCalls);
            SnapshotObserved.TrySetResult();
            return new(Started ? Clip : [], 0);
        }
    }

    private sealed class FakeTrimmer : ISpeechTrimmer
    {
        public Func<float[], float[]> Impl = s => s;
        public float[] Trim(float[] samples) => Impl(samples);

        public SpeechSpan[] Analyze(float[] samples) =>
            samples.Length == 0 ? [] : [new SpeechSpan(0, samples.Length)];
    }

    private sealed class FakeTranscriber : ITranscriber
    {
        public string Text = "hello world";
        public int Calls;

        public string Transcribe(float[] samples)
        {
            Calls++;
            return Text;
        }
    }

    private static float[] Seconds(double s) => new float[(int)(s * DictationService.SampleRate)];

    private static DictationService Service(IAudioRecorder rec, FakeTranscriber tr, FakeTrimmer? trim = null) =>
        new(rec, trim ?? new FakeTrimmer(), tr, NullLogger.Instance);

    [Fact]
    public async Task HoldAndRelease_TranscribesTrimmedClip()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var tr = new FakeTranscriber();
        var svc = Service(rec, tr);

        Assert.True(svc.Start());
        Assert.True(rec.Started);
        var result = await svc.StopAsync();

        Assert.Equal("hello world", result.Text);
        Assert.Equal(1, tr.Calls);
        Assert.Equal(DictationState.Idle, svc.State);
    }

    [Fact]
    public async Task ShortClip_SkipsTranscriber()
    {
        var rec = new FakeRecorder { Clip = Seconds(0.1) };
        var tr = new FakeTranscriber();
        var svc = Service(rec, tr);

        svc.Start();
        var result = await svc.StopAsync();

        Assert.Equal("", result.Text);
        Assert.Equal(0, tr.Calls);
    }

    [Fact]
    public async Task NoSpeech_SkipsTranscriber()
    {
        var rec = new FakeRecorder { Clip = Seconds(3) };
        var tr = new FakeTranscriber();
        var trim = new FakeTrimmer { Impl = _ => [] };
        var svc = Service(rec, tr, trim);

        svc.Start();
        var result = await svc.StopAsync();

        Assert.Equal("", result.Text);
        Assert.Equal(0, tr.Calls);
    }

    [Fact]
    public void DoubleStart_SecondReturnsFalse()
    {
        var rec = new FakeRecorder { Clip = Seconds(1) };
        var svc = Service(rec, new FakeTranscriber());

        Assert.True(svc.Start());
        Assert.False(svc.Start());
    }

    [Fact]
    public async Task StopWithoutStart_ReturnsEmpty()
    {
        var svc = Service(new FakeRecorder(), new FakeTranscriber());
        var result = await svc.StopAsync();
        Assert.Equal("", result.Text);
    }

    [Fact]
    public void RecorderFailure_SurfacesTypedErrorAndStaysIdle()
    {
        var rec = new FakeRecorder { ThrowOnStart = true };
        var svc = Service(rec, new FakeTranscriber());

        Assert.Throws<DictationException>(() => svc.Start());
        Assert.Equal(DictationState.Idle, svc.State);
    }

    [Fact]
    public async Task StopWithCanceledToken_ReturnsEmptyAndIdle()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var tr = new FakeTranscriber();
        var svc = Service(rec, tr);

        svc.Start();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await svc.StopAsync(cts.Token);

        Assert.Equal("", result.Text);
        Assert.Equal(0, tr.Calls);
        Assert.Equal(DictationState.Idle, svc.State);
    }

    [Fact]
    public async Task RecorderStopFailure_ReturnsToIdle()
    {
        var rec = new ThrowingStopRecorder();
        var svc = Service(rec, new FakeTranscriber());

        svc.Start();
        await Assert.ThrowsAsync<DictationException>(() => svc.StopAsync());
        Assert.Equal(DictationState.Idle, svc.State);
    }

    private sealed class ThrowingStopRecorder : IAudioRecorder
    {
        public void Start() { }
        public float[] Stop() => throw new DictationException("stream teardown failed");
        public AudioSnapshot Snapshot(double maxSeconds) => new([], 0);
    }

    private sealed class CountingTranscriber : ITranscriber
    {
        private int _calls;
        public string Transcribe(float[] samples) => "t" + Interlocked.Increment(ref _calls);
    }

    private sealed class BlockingTranscriber : ITranscriber
    {
        public ManualResetEventSlim Entered = new(false);
        public ManualResetEventSlim Release = new(false);
        public volatile bool Exited;
        private bool _first = true;

        public string Transcribe(float[] samples)
        {
            if (_first)
            {
                _first = false;
                Entered.Set();
                if (!Release.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("blocking transcriber was not released");
                Exited = true;
                return "partial";
            }
            return "final";
        }
    }

    private static async Task ShutdownAsync(DictationService service)
    {
        await service.ShutdownAsync()
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ShutdownAsync_AwaitsInFlightDecodeWithoutBlockingCaller()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var tr = new BlockingTranscriber();
        var svc = new DictationService(
            rec,
            new FakeTrimmer(),
            tr,
            NullLogger.Instance,
            TimeSpan.FromMilliseconds(10));
        svc.PartialTranscript = _ => { };

        Assert.True(svc.Start());
        Assert.True(tr.Entered.Wait(TimeSpan.FromSeconds(5)));

        var shutdown = svc.ShutdownAsync();

        Assert.False(shutdown.IsCompleted);
        tr.Release.Set();
        await shutdown.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StopAsync_WaitsForInFlightPartialDecode()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var tr = new BlockingTranscriber();
        var svc = new DictationService(rec, new FakeTrimmer(), tr, NullLogger.Instance, TimeSpan.FromMilliseconds(10));
        svc.PartialTranscript = _ => { };

        Assert.True(svc.Start());
        Assert.True(tr.Entered.Wait(TimeSpan.FromSeconds(5)));
        var stopTask = svc.StopAsync();
        DictationResult? result = null;
        try
        {
            Assert.Equal(DictationState.Transcribing, svc.State);
            Assert.False(stopTask.IsCompleted);
        }
        finally
        {
            tr.Release.Set();
            result = await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal("final", result!.Text);
    }

    [Fact]
    public async Task ShutdownAsync_WaitsForInFlightPartialDecode()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var tr = new BlockingTranscriber();
        var svc = new DictationService(rec, new FakeTrimmer(), tr, NullLogger.Instance, TimeSpan.FromMilliseconds(10));
        svc.PartialTranscript = _ => { };

        Assert.True(svc.Start());
        Assert.True(tr.Entered.Wait(TimeSpan.FromSeconds(5)));
        var shutdown = svc.ShutdownAsync();
        try
        {
            Assert.False(shutdown.IsCompleted);
        }
        finally
        {
            tr.Release.Set();
            await shutdown.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.True(tr.Exited);
    }

    [Fact]
    public async Task ShutdownAsync_WaitsForInFlightFinalDecode()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var tr = new BlockingTranscriber();
        var svc = new DictationService(rec, new FakeTrimmer(), tr, NullLogger.Instance);

        Assert.True(svc.Start());
        var stopTask = svc.StopAsync();
        Assert.True(tr.Entered.Wait(TimeSpan.FromSeconds(5)));
        var shutdown = svc.ShutdownAsync();
        DictationResult? result = null;
        try
        {
            Assert.False(shutdown.IsCompleted);
        }
        finally
        {
            tr.Release.Set();
            await shutdown.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            result = await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.True(tr.Exited);
        Assert.Equal("partial", result!.Text);
    }

    [Fact]
    public async Task ShutdownAsync_SkipsFinalDecodeQueuedBehindBlockedPartial()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var tr = new BlockingTranscriber();
        var svc = new DictationService(rec, new FakeTrimmer(), tr, NullLogger.Instance, TimeSpan.FromMilliseconds(10));
        svc.PartialTranscript = _ => { };

        Assert.True(svc.Start());
        Assert.True(tr.Entered.Wait(TimeSpan.FromSeconds(5)));
        var stopTask = svc.StopAsync();
        var shutdown = svc.ShutdownAsync();
        DictationResult? result = null;
        try
        {
            Assert.Equal(DictationState.Transcribing, svc.State);
            Assert.False(shutdown.IsCompleted);
            Assert.False(stopTask.IsCompleted);
        }
        finally
        {
            tr.Release.Set();
            await shutdown.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            result = await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal("", result!.Text);
    }

    [Fact]
    public async Task StopAsync_DrainsPartialLoopBeforeReturning()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var svc = new DictationService(rec, new FakeTrimmer(), new CountingTranscriber(), NullLogger.Instance, TimeSpan.FromMilliseconds(10));
        var emissions = 0;
        var firstEmission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = 0;
        var postStopEmission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        svc.PartialTranscript = _ =>
        {
            Interlocked.Increment(ref emissions);
            firstEmission.TrySetResult();
            if (Volatile.Read(ref stopped) != 0)
                postStopEmission.TrySetResult();
        };

        Assert.True(svc.Start());
        try
        {
            await firstEmission.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await svc.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var settled = Volatile.Read(ref emissions);
            Volatile.Write(ref stopped, 1);
            await Assert.ThrowsAsync<TimeoutException>(
                () => postStopEmission.Task.WaitAsync(TimeSpan.FromMilliseconds(100)));
            Assert.Equal(settled, Volatile.Read(ref emissions));
        }
        finally
        {
            if (svc.State == DictationState.Recording)
                await svc.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await ShutdownAsync(svc);
        }
    }

    [Fact]
    public async Task Shutdown_StopsPartialLoopWithoutStop()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var svc = new DictationService(rec, new FakeTrimmer(), new CountingTranscriber(), NullLogger.Instance, TimeSpan.FromMilliseconds(10));
        var emissions = 0;
        var firstEmission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = 0;
        var postStopEmission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        svc.PartialTranscript = _ =>
        {
            Interlocked.Increment(ref emissions);
            firstEmission.TrySetResult();
            if (Volatile.Read(ref stopped) != 0)
                postStopEmission.TrySetResult();
        };

        Assert.True(svc.Start());
        try
        {
            await firstEmission.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await ShutdownAsync(svc);
            var settled = Volatile.Read(ref emissions);
            Volatile.Write(ref stopped, 1);
            await Assert.ThrowsAsync<TimeoutException>(
                () => postStopEmission.Task.WaitAsync(TimeSpan.FromMilliseconds(100)));
            Assert.Equal(settled, Volatile.Read(ref emissions));
        }
        finally
        {
            await ShutdownAsync(svc);
        }
    }

    [Fact]
    public async Task PartialTranscript_EmitsWhileRecordingAndFinalMatches()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var tr = new FakeTranscriber();
        var svc = new DictationService(rec, new FakeTrimmer(), tr, NullLogger.Instance, TimeSpan.FromMilliseconds(15));
        var partials = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        svc.PartialTranscript = t => partials.TrySetResult(t);

        Assert.True(svc.Start());
        var stopped = false;
        try
        {
            var partial = await partials.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("hello world", partial);

            var result = await svc.StopAsync();
            stopped = true;
            Assert.Equal("hello world", result.Text);
            Assert.Equal(DictationState.Idle, svc.State);
        }
        finally
        {
            if (!stopped)
                await svc.StopAsync();
            await ShutdownAsync(svc);
        }
    }

    [Fact]
    public async Task NoPartialSubscriber_NeverSnapshotsOrDecodesEarly()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var tr = new FakeTranscriber();
        var svc = new DictationService(rec, new FakeTrimmer(), tr, NullLogger.Instance, TimeSpan.FromMilliseconds(15));

        Assert.True(svc.Start());
        DictationResult? result = null;
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(
                () => rec.SnapshotObserved.Task.WaitAsync(TimeSpan.FromMilliseconds(150)));
            Assert.Equal(0, rec.SnapshotCalls);
            Assert.Equal(0, tr.Calls);
        }
        finally
        {
            result = await svc.StopAsync();
            await ShutdownAsync(svc);
        }
        Assert.Equal(1, tr.Calls);
        Assert.Equal("hello world", result!.Text);
    }
}
