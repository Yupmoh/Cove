using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Dictation;
using Cove.Engine.Dictation;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class DictationCommandTests
{
    private const int SampleRate = 16000;

    [Fact]
    public void AudioBounds_MatchFinalAndSnapshotWindowContracts()
    {
        Assert.Equal(
            7_680_000,
            DictationTranscriptionRuntime.MaxFinalAudioBytes);
        Assert.Equal(
            (int)(PartialTranscriptTracker.SnapshotWindowSeconds
                * SampleRate
                * sizeof(float)),
            DictationTranscriptionRuntime.MaxPartialAudioBytes);
    }

    [Fact]
    public async Task StatusAndEnsureModel_UseEngineProvisioningAndPublishModelEvents()
    {
        var events = new EventLog();
        var provisioner = new FakeProvisioner();
        await using var runtime = CreateRuntime(
            provisioner,
            new FakeTrimmer(),
            new FakeTranscriber(),
            events);

        var initial = await RouteAsync(runtime, "dictation.status");

        Assert.True(initial.Ok);
        Assert.Equal(
            """{"state":"idle","modelReady":false}""",
            initial.Data!.Value.GetRawText());

        var ensure = await RouteAsync(runtime, "dictation.ensure-model");

        Assert.True(ensure.Ok);
        Assert.Equal(
            """{"started":true}""",
            ensure.Data!.Value.GetRawText());
        await events.ModelReady.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(
            [
                ("dictation.progress", """{"percent":0.25}"""),
                ("dictation.model", """{"ready":true}"""),
            ],
            events.Items);

        var ready = await RouteAsync(runtime, "dictation.status");
        Assert.Equal(
            """{"state":"idle","modelReady":true}""",
            ready.Data!.Value.GetRawText());
    }

    [Fact]
    public async Task BeginStartedPartialStop_PreservesResultsEventsAndResetsState()
    {
        var events = new EventLog();
        var provisioner = new FakeProvisioner("/models/asr");
        var trimmer = new FakeTrimmer
        {
            AnalyzeResult = [new SpeechSpan(0, 3200)],
        };
        var transcriber = new FakeTranscriber("hello", "hello world");
        await using var runtime = CreateRuntime(
            provisioner,
            trimmer,
            transcriber,
            events);

        var begin = await RouteAsync(runtime, "dictation.begin");
        var sessionId = begin.Data!.Value
            .GetProperty("sessionId")
            .GetString();

        Assert.True(begin.Ok);
        Assert.False(string.IsNullOrWhiteSpace(sessionId));

        var started = await RouteAsync(
            runtime,
            "dictation.started",
            new DictationSessionParams(sessionId!),
            CoveJsonContext.Default.DictationSessionParams);
        var partial = await RouteAsync(
            runtime,
            "dictation.partial",
            new DictationPartialParams(
                sessionId!,
                AudioPayload(
                    Enumerable.Repeat(0.5f, SampleRate).ToArray(),
                    72)),
            CoveJsonContext.Default.DictationPartialParams);
        var stop = await RouteAsync(
            runtime,
            "dictation.stop",
            new DictationStopParams(
                sessionId!,
                AudioPayload(
                    Enumerable.Repeat(0.25f, SampleRate / 2).ToArray())),
            CoveJsonContext.Default.DictationStopParams);

        Assert.True(started.Ok);
        Assert.True(partial.Ok);
        Assert.True(stop.Ok);
        Assert.Equal("hello world", stop.Data!.Value.GetProperty("text").GetString());
        Assert.Equal(0.5, stop.Data.Value.GetProperty("audioSeconds").GetDouble());
        Assert.True(stop.Data.Value.GetProperty("transcribeMs").GetInt64() >= 0);
        Assert.Equal(1, trimmer.AnalyzeCount);
        Assert.Equal(1, trimmer.TrimCount);
        Assert.Equal(2, transcriber.TranscribeCount);
        Assert.Equal(
            [
                ("dictation.state", """{"recording":true}"""),
                ("dictation.partial", """{"text":"hello"}"""),
                ("dictation.state", """{"recording":false}"""),
            ],
            events.Items);

        var status = await RouteAsync(runtime, "dictation.status");
        Assert.Equal(
            """{"state":"idle","modelReady":true}""",
            status.Data!.Value.GetRawText());
    }

    [Fact]
    public async Task Begin_ReplacesStaleSessionAndRejectsItsAudioBeforeDecode()
    {
        var events = new EventLog();
        var trimmer = new FakeTrimmer
        {
            AnalyzeResult = [new SpeechSpan(0, 1)],
        };
        var transcriber = new FakeTranscriber("unused");
        await using var runtime = CreateRuntime(
            new FakeProvisioner("/models/asr"),
            trimmer,
            transcriber,
            events);

        var first = await BeginAsync(runtime);
        await StartedAsync(runtime, first);
        var second = await BeginAsync(runtime);

        Assert.NotEqual(first, second);

        var stale = await RouteAsync(
            runtime,
            "dictation.partial",
            new DictationPartialParams(first, AudioPayload([1f])),
            CoveJsonContext.Default.DictationPartialParams);

        Assert.False(stale.Ok);
        Assert.Equal("invalid_params", stale.Error!.Code);
        Assert.Equal(0, trimmer.AnalyzeCount);
        Assert.Equal(0, transcriber.TranscribeCount);

        var cancelStale = await CancelAsync(runtime, first);
        var cancelCurrent = await CancelAsync(runtime, second);
        var cancelAgain = await CancelAsync(runtime, second);

        Assert.True(cancelStale.Ok);
        Assert.True(cancelCurrent.Ok);
        Assert.True(cancelAgain.Ok);
        Assert.Equal(
            ("dictation.state", """{"recording":false}"""),
            events.Items[^1]);
    }

    [Theory]
    [InlineData("sample-rate")]
    [InlineData("alignment")]
    [InlineData("final-size")]
    public async Task Stop_RejectsInvalidAudioBeforeDecodeAndReturnsToIdle(
        string invalidCase)
    {
        var events = new EventLog();
        var trimmer = new FakeTrimmer();
        var transcriber = new FakeTranscriber("unused");
        await using var runtime = CreateRuntime(
            new FakeProvisioner("/models/asr"),
            trimmer,
            transcriber,
            events);
        var sessionId = await BeginAsync(runtime);
        await StartedAsync(runtime, sessionId);
        var audio = invalidCase switch
        {
            "sample-rate" => new DictationAudioPayload([0, 0, 0, 0], 8000),
            "alignment" => new DictationAudioPayload([0, 0, 0], SampleRate),
            "final-size" => new DictationAudioPayload(
                new byte[DictationTranscriptionRuntime.MaxFinalAudioBytes + 4],
                SampleRate),
            _ => throw new InvalidOperationException(invalidCase),
        };

        var response = await RouteAsync(
            runtime,
            "dictation.stop",
            new DictationStopParams(sessionId, audio),
            CoveJsonContext.Default.DictationStopParams);

        Assert.False(response.Ok);
        Assert.Equal("invalid_params", response.Error!.Code);
        Assert.Equal(0, trimmer.TrimCount);
        Assert.Equal(0, transcriber.TranscribeCount);
        var status = await RouteAsync(runtime, "dictation.status");
        Assert.Equal(
            """{"state":"idle","modelReady":true}""",
            status.Data!.Value.GetRawText());
    }

    [Fact]
    public async Task Partial_RejectsOversizePayloadBeforeDecode()
    {
        var trimmer = new FakeTrimmer();
        var transcriber = new FakeTranscriber("unused");
        await using var runtime = CreateRuntime(
            new FakeProvisioner("/models/asr"),
            trimmer,
            transcriber,
            new EventLog());
        var sessionId = await BeginAsync(runtime);
        await StartedAsync(runtime, sessionId);

        var response = await RouteAsync(
            runtime,
            "dictation.partial",
            new DictationPartialParams(
                sessionId,
                new DictationAudioPayload(
                    new byte[DictationTranscriptionRuntime.MaxPartialAudioBytes + 4],
                    SampleRate)),
            CoveJsonContext.Default.DictationPartialParams);

        Assert.False(response.Ok);
        Assert.Equal("invalid_params", response.Error!.Code);
        Assert.Equal(0, trimmer.AnalyzeCount);
        Assert.Equal(0, transcriber.TranscribeCount);
    }

    [Fact]
    public async Task DisposeAsync_CancelsAndAwaitsModelProvisioning()
    {
        var provisioner = new BlockingProvisioner();
        var runtime = CreateRuntime(
            provisioner,
            new FakeTrimmer(),
            new FakeTranscriber(),
            new EventLog());

        var ensure = await RouteAsync(runtime, "dictation.ensure-model");
        Assert.True(ensure.Ok);
        await provisioner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await runtime.DisposeAsync();

        await provisioner.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DisposeAsync_WaitsForDecodeAndDisposesNativeResourcesInReverseOrder()
    {
        var order = new List<string>();
        var trimmer = new FakeTrimmer(order);
        var transcriber = new BlockingTranscriber(order);
        var runtime = CreateRuntime(
            new FakeProvisioner("/models/asr"),
            trimmer,
            transcriber,
            new EventLog());
        var sessionId = await BeginAsync(runtime);
        await StartedAsync(runtime, sessionId);

        var stop = Task.Run(async () => await RouteAsync(
            runtime,
            "dictation.stop",
            new DictationStopParams(sessionId, AudioPayload([0.5f])),
            CoveJsonContext.Default.DictationStopParams));
        await transcriber.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var dispose = runtime.DisposeAsync().AsTask();
        Assert.False(dispose.IsCompleted);

        transcriber.Release.TrySetResult();
        Assert.True((await stop).Ok);
        await dispose;

        Assert.Equal(["transcriber", "trimmer"], order);
    }

    private static DictationTranscriptionRuntime CreateRuntime(
        IDictationModelProvisioner provisioner,
        ISpeechTrimmer trimmer,
        ITranscriber transcriber,
        EventLog events) =>
        new(
            provisioner,
            _ => trimmer,
            _ => transcriber,
            events.Publish,
            NullLogger.Instance);

    private static async Task<string> BeginAsync(
        DictationTranscriptionRuntime runtime)
    {
        var response = await RouteAsync(runtime, "dictation.begin");
        Assert.True(response.Ok);
        return response.Data!.Value.GetProperty("sessionId").GetString()!;
    }

    private static Task<ControlResponse> StartedAsync(
        DictationTranscriptionRuntime runtime,
        string sessionId) =>
        RouteAsync(
            runtime,
            "dictation.started",
            new DictationSessionParams(sessionId),
            CoveJsonContext.Default.DictationSessionParams);

    private static Task<ControlResponse> CancelAsync(
        DictationTranscriptionRuntime runtime,
        string sessionId) =>
        RouteAsync(
            runtime,
            "dictation.cancel",
            new DictationSessionParams(sessionId),
            CoveJsonContext.Default.DictationSessionParams);

    private static async Task<ControlResponse> RouteAsync(
        DictationTranscriptionRuntime runtime,
        string route)
    {
        var response = await EngineCommandRouter.RouteAsync(
            new ControlRequest("1", $"cove://commands/{route}"),
            dictation: runtime);
        return Assert.IsType<ControlResponse>(response);
    }

    private static async Task<ControlResponse> RouteAsync<T>(
        DictationTranscriptionRuntime runtime,
        string route,
        T parameters,
        JsonTypeInfo<T> typeInfo)
    {
        var request = new ControlRequest(
            "1",
            $"cove://commands/{route}",
            JsonSerializer.SerializeToElement(parameters, typeInfo));
        var response = await EngineCommandRouter.RouteAsync(
            request,
            dictation: runtime);
        return Assert.IsType<ControlResponse>(response);
    }

    private static DictationAudioPayload AudioPayload(
        float[] samples,
        int offset = 0)
    {
        var bytes = new byte[samples.Length * sizeof(float)];
        for (var index = 0; index < samples.Length; index++)
        {
            var bits = BitConverter.SingleToInt32Bits(samples[index]);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                bytes.AsSpan(index * sizeof(float)),
                bits);
        }
        return new DictationAudioPayload(bytes, SampleRate, offset);
    }

    private sealed class EventLog
    {
        private readonly object _sync = new();

        public List<(string Channel, string Payload)> Items { get; } = [];

        public TaskCompletionSource ModelReady { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Publish(string channel, JsonElement payload)
        {
            lock (_sync)
                Items.Add((channel, payload.GetRawText()));
            if (channel == "dictation.model"
                && payload.TryGetProperty("ready", out var ready)
                && ready.GetBoolean())
            {
                ModelReady.TrySetResult();
            }
        }
    }

    private class FakeProvisioner : IDictationModelProvisioner
    {
        private string? _modelDirectory;

        public FakeProvisioner(string? modelDirectory = null)
        {
            _modelDirectory = modelDirectory;
        }

        public string VadPath => "/models/vad.onnx";

        public string? TryGetModelDir() => Volatile.Read(ref _modelDirectory);

        public virtual Task<string> EnsureModelAsync(
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(0.25);
            Volatile.Write(ref _modelDirectory, "/models/asr");
            return Task.FromResult("/models/asr");
        }
    }

    private sealed class BlockingProvisioner : FakeProvisioner
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<string> EnsureModelAsync(
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("unreachable");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                Cancelled.TrySetResult();
                throw;
            }
        }
    }

    private sealed class FakeTrimmer : ISpeechTrimmer, IDisposable
    {
        private readonly List<string>? _disposeOrder;

        public FakeTrimmer(List<string>? disposeOrder = null)
        {
            _disposeOrder = disposeOrder;
        }

        public SpeechSpan[] AnalyzeResult { get; init; } = [];

        public int AnalyzeCount { get; private set; }

        public int TrimCount { get; private set; }

        public float[] Trim(float[] samples)
        {
            TrimCount++;
            return samples;
        }

        public SpeechSpan[] Analyze(float[] samples)
        {
            AnalyzeCount++;
            return AnalyzeResult;
        }

        public void Dispose() => _disposeOrder?.Add("trimmer");
    }

    private class FakeTranscriber : ITranscriber, IDisposable
    {
        private readonly Queue<string> _results;

        public FakeTranscriber(params string[] results)
        {
            _results = new Queue<string>(results);
        }

        public int TranscribeCount { get; private set; }

        public virtual string Transcribe(float[] samples)
        {
            TranscribeCount++;
            return _results.Count == 0 ? "" : _results.Dequeue();
        }

        public virtual void Dispose() { }
    }

    private sealed class BlockingTranscriber : FakeTranscriber
    {
        private readonly List<string> _disposeOrder;

        public BlockingTranscriber(List<string> disposeOrder)
        {
            _disposeOrder = disposeOrder;
        }

        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override string Transcribe(float[] samples)
        {
            Entered.TrySetResult();
            Release.Task.GetAwaiter().GetResult();
            return "done";
        }

        public override void Dispose() => _disposeOrder.Add("transcriber");
    }
}
