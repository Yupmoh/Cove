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

        public AudioSnapshot Snapshot(double maxSeconds) => new(Started ? Clip : [], 0);
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
                Release.Wait(TimeSpan.FromSeconds(5));
                Exited = true;
                return "partial";
            }
            return "final";
        }
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
        await Task.Delay(50);
        Assert.False(stopTask.IsCompleted);
        tr.Release.Set();
        var result = await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("final", result.Text);
    }

    [Fact]
    public void Shutdown_WaitsForInFlightPartialDecode()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var tr = new BlockingTranscriber();
        var svc = new DictationService(rec, new FakeTrimmer(), tr, NullLogger.Instance, TimeSpan.FromMilliseconds(10));
        svc.PartialTranscript = _ => { };

        Assert.True(svc.Start());
        Assert.True(tr.Entered.Wait(TimeSpan.FromSeconds(5)));
        _ = Task.Run(() =>
        {
            Thread.Sleep(100);
            tr.Release.Set();
        });
        svc.Shutdown();
        Assert.True(tr.Exited);
    }

    [Fact]
    public async Task Shutdown_WaitsForInFlightFinalDecode()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var tr = new BlockingTranscriber();
        var svc = new DictationService(rec, new FakeTrimmer(), tr, NullLogger.Instance);

        Assert.True(svc.Start());
        var stopTask = svc.StopAsync();
        Assert.True(tr.Entered.Wait(TimeSpan.FromSeconds(5)));
        _ = Task.Run(() =>
        {
            Thread.Sleep(100);
            tr.Release.Set();
        });
        svc.Shutdown();
        Assert.True(tr.Exited);
        var result = await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("partial", result.Text);
    }

    [Fact]
    public async Task Shutdown_SkipsFinalDecodeQueuedBehindBlockedPartial()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var tr = new BlockingTranscriber();
        var svc = new DictationService(rec, new FakeTrimmer(), tr, NullLogger.Instance, TimeSpan.FromMilliseconds(10));
        svc.PartialTranscript = _ => { };

        Assert.True(svc.Start());
        Assert.True(tr.Entered.Wait(TimeSpan.FromSeconds(5)));
        var stopTask = svc.StopAsync();
        var shutdownTask = Task.Run(() => svc.Shutdown());
        await Task.Delay(50);
        tr.Release.Set();
        await shutdownTask.WaitAsync(TimeSpan.FromSeconds(5));
        var result = await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("", result.Text);
    }

    [Fact]
    public async Task StopAsync_DrainsPartialLoopBeforeReturning()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var svc = new DictationService(rec, new FakeTrimmer(), new CountingTranscriber(), NullLogger.Instance, TimeSpan.FromMilliseconds(10));
        var emissions = 0;
        svc.PartialTranscript = _ => Interlocked.Increment(ref emissions);

        Assert.True(svc.Start());
        await Task.Delay(60);
        await svc.StopAsync();
        var settled = Volatile.Read(ref emissions);
        await Task.Delay(80);
        Assert.Equal(settled, Volatile.Read(ref emissions));
    }

    [Fact]
    public async Task Shutdown_StopsPartialLoopWithoutStop()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var svc = new DictationService(rec, new FakeTrimmer(), new CountingTranscriber(), NullLogger.Instance, TimeSpan.FromMilliseconds(10));
        var emissions = 0;
        svc.PartialTranscript = _ => Interlocked.Increment(ref emissions);

        Assert.True(svc.Start());
        await Task.Delay(60);
        svc.Shutdown();
        var settled = Volatile.Read(ref emissions);
        await Task.Delay(80);
        Assert.Equal(settled, Volatile.Read(ref emissions));
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
        var partial = await partials.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("hello world", partial);

        var result = await svc.StopAsync();
        Assert.Equal("hello world", result.Text);
        Assert.Equal(DictationState.Idle, svc.State);
    }

    [Fact]
    public async Task NoPartialSubscriber_NeverSnapshotsOrDecodesEarly()
    {
        var rec = new FakeRecorder { Clip = Seconds(2) };
        var tr = new FakeTranscriber();
        var svc = new DictationService(rec, new FakeTrimmer(), tr, NullLogger.Instance, TimeSpan.FromMilliseconds(15));

        Assert.True(svc.Start());
        await Task.Delay(120);
        Assert.Equal(0, tr.Calls);
        var result = await svc.StopAsync();
        Assert.Equal(1, tr.Calls);
        Assert.Equal("hello world", result.Text);
    }
}
