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
    }

    private sealed class FakeTrimmer : ISpeechTrimmer
    {
        public Func<float[], float[]> Impl = s => s;
        public float[] Trim(float[] samples) => Impl(samples);
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
        public float[] Stop() => throw new DictationException("device lost");
    }
}
