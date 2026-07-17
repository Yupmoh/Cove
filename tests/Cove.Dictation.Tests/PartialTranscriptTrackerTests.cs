using Cove.Dictation;
using Xunit;

namespace Cove.Dictation.Tests;

public sealed class PartialTranscriptTrackerTests
{
    private const int Rate = DictationService.SampleRate;

    private sealed class FakeSpanTrimmer : ISpeechTrimmer
    {
        public Func<float[], SpeechSpan[]> Spans = _ => [];
        public List<int> TailLengths = new();

        public SpeechSpan[] Analyze(float[] samples)
        {
            TailLengths.Add(samples.Length);
            return Spans(samples);
        }

        public float[] Trim(float[] samples) => samples;
    }

    private sealed class QueueTranscriber : ITranscriber
    {
        public Queue<string> Texts = new();
        public List<int> SampleLengths = new();
        public int Calls;

        public string Transcribe(float[] samples)
        {
            Calls++;
            SampleLengths.Add(samples.Length);
            return Texts.Count > 0 ? Texts.Dequeue() : "";
        }
    }

    private static float[] Seconds(double s) => new float[(int)(s * Rate)];

    private static int S(double s) => (int)(s * Rate);

    private static AudioSnapshot Snap(double seconds, double offsetSeconds = 0) =>
        new(Seconds(seconds), S(offsetSeconds));

    [Fact]
    public void OpenSegment_ProducesPreviewAndRedecodesOnGrowth()
    {
        var trimmer = new FakeSpanTrimmer { Spans = s => [new SpeechSpan(0, s.Length)] };
        var transcriber = new QueueTranscriber { Texts = new(["hel", "hello"]) };
        var tracker = new PartialTranscriptTracker(trimmer, transcriber);

        Assert.Equal("hel", tracker.Advance(Snap(1)));
        Assert.Equal("hello", tracker.Advance(Snap(2)));
        Assert.Equal(2, transcriber.Calls);
    }

    [Fact]
    public void UnchangedPartial_ReturnsNull()
    {
        var trimmer = new FakeSpanTrimmer { Spans = s => [new SpeechSpan(0, s.Length)] };
        var transcriber = new QueueTranscriber { Texts = new(["same", "same"]) };
        var tracker = new PartialTranscriptTracker(trimmer, transcriber);

        Assert.Equal("same", tracker.Advance(Snap(1)));
        Assert.Null(tracker.Advance(Snap(2)));
    }

    [Fact]
    public void ClosedSegment_CommitsOnceAndNeverRedecodes()
    {
        var trimmer = new FakeSpanTrimmer
        {
            Spans = s => s.Length == S(5) ? [new SpeechSpan(0, S(3.5))] : [],
        };
        var transcriber = new QueueTranscriber { Texts = new(["one"]) };
        var tracker = new PartialTranscriptTracker(trimmer, transcriber);

        Assert.Equal("one", tracker.Advance(Snap(5)));
        Assert.Null(tracker.Advance(Snap(6)));
        Assert.Equal(1, transcriber.Calls);
        Assert.Equal(S(6) - S(3.5), trimmer.TailLengths[1]);
    }

    [Fact]
    public void CommittedAndOpen_JoinInOrder()
    {
        var trimmer = new FakeSpanTrimmer
        {
            Spans = s => s.Length == S(5)
                ? [new SpeechSpan(0, S(3.5))]
                : [new SpeechSpan(S(0.5), s.Length)],
        };
        var transcriber = new QueueTranscriber { Texts = new(["one", "two"]) };
        var tracker = new PartialTranscriptTracker(trimmer, transcriber);

        Assert.Equal("one", tracker.Advance(Snap(5)));
        Assert.Equal("one two", tracker.Advance(Snap(7)));
    }

    [Fact]
    public void SilentTail_AdvancesOffsetWithoutDecoding()
    {
        var trimmer = new FakeSpanTrimmer();
        var transcriber = new QueueTranscriber();
        var tracker = new PartialTranscriptTracker(trimmer, transcriber);

        Assert.Null(tracker.Advance(Snap(10)));
        Assert.Null(tracker.Advance(Snap(11)));
        Assert.Equal(0, transcriber.Calls);
        Assert.True(trimmer.TailLengths[1] <= S(2));
    }

    [Fact]
    public void OpenPreview_IsCappedToTrailingWindow()
    {
        var trimmer = new FakeSpanTrimmer { Spans = s => [new SpeechSpan(0, s.Length)] };
        var transcriber = new QueueTranscriber { Texts = new(["long"]) };
        var tracker = new PartialTranscriptTracker(trimmer, transcriber);

        tracker.Advance(Snap(40));
        Assert.Single(transcriber.SampleLengths);
        Assert.Equal(S(PartialTranscriptTracker.OpenPreviewCapSeconds), transcriber.SampleLengths[0]);
    }

    [Fact]
    public void EmptyAndShrunkSnapshots_AreIgnored()
    {
        var trimmer = new FakeSpanTrimmer { Spans = s => [new SpeechSpan(0, s.Length)] };
        var transcriber = new QueueTranscriber { Texts = new(["one"]) };
        var tracker = new PartialTranscriptTracker(trimmer, transcriber);

        Assert.Null(tracker.Advance(new AudioSnapshot([], 0)));
        Assert.Equal("one", tracker.Advance(Snap(1)));
        Assert.Null(tracker.Advance(new AudioSnapshot([], 0)));
        Assert.Equal(1, transcriber.Calls);
    }

    [Fact]
    public void WindowedSnapshot_AdoptsWindowStartWhenOffsetSlidPast()
    {
        var trimmer = new FakeSpanTrimmer { Spans = s => [new SpeechSpan(0, s.Length)] };
        var transcriber = new QueueTranscriber { Texts = new(["tail"]) };
        var tracker = new PartialTranscriptTracker(trimmer, transcriber);

        Assert.Equal("tail", tracker.Advance(Snap(2, 30)));
        Assert.Equal(S(2), trimmer.TailLengths[0]);
    }
}
