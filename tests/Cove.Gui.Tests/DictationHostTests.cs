using System.Text.Json;
using Cove.Dictation;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Gui.Tests;

public sealed class DictationHostTests
{
    [Fact]
    public async Task StatusAndEnsureModel_ForwardEngineResultShapes()
    {
        var engine = new FakeDictationEngine();
        engine.Respond(
            "cove://commands/dictation.status",
            """{"state":"idle","modelReady":false}""");
        engine.Respond(
            "cove://commands/dictation.ensure-model",
            """{"started":true}""");
        await using var host = CreateHost(engine, new FakeRecorder());

        var status = await host.Status();
        var ensure = await host.EnsureModel();

        Assert.Equal(
            """{"state":"idle","modelReady":false}""",
            status);
        Assert.Equal("""{"started":true}""", ensure);
        Assert.Equal(
            [
                "cove://commands/dictation.status",
                "cove://commands/dictation.ensure-model",
            ],
            engine.Requests.Select(request => request.Uri));
    }

    [Fact]
    public async Task StartPartialStop_CapturesLocallyAndSendsLittleEndianFloat32Payloads()
    {
        var recorder = new FakeRecorder
        {
            SnapshotResult = new AudioSnapshot([1f, -2.5f], 41),
            StopResult = [0.5f, -0.25f],
        };
        var engine = new FakeDictationEngine();
        engine.Respond(
            "cove://commands/dictation.begin",
            """{"sessionId":"session-1"}""");
        engine.Respond(
            "cove://commands/dictation.started",
            "{}");
        engine.Respond(
            "cove://commands/dictation.partial",
            "{}");
        engine.Respond(
            "cove://commands/dictation.stop",
            """{"text":"hello world","audioSeconds":0.5,"transcribeMs":12}""");
        await using var host = CreateHost(
            engine,
            recorder,
            TimeSpan.FromMilliseconds(10));

        var start = await host.StartDictation();

        Assert.Equal("""{"ok":true}""", start);
        Assert.Equal(1, recorder.StartCount);
        var partialRequest = await engine.PartialReceived.Task
            .WaitAsync(TimeSpan.FromSeconds(5));
        var partial = JsonSerializer.Deserialize(
            partialRequest.Params!.Value,
            CoveJsonContext.Default.DictationPartialParams)!;
        Assert.Equal("session-1", partial.SessionId);
        Assert.Equal(16000, partial.Audio.SampleRate);
        Assert.Equal(41, partial.Audio.Offset);
        Assert.Equal(
            [0x00, 0x00, 0x80, 0x3f, 0x00, 0x00, 0x20, 0xc0],
            partial.Audio.Pcm);
        Assert.Equal(
            "AACAPwAAIMA=",
            partialRequest.Params.Value
                .GetProperty("audio")
                .GetProperty("pcm")
                .GetString());

        var stop = await host.StopDictation();

        Assert.Equal(
            """{"text":"hello world","audioSeconds":0.5,"transcribeMs":12}""",
            stop);
        Assert.Equal(1, recorder.StopCount);
        var stopRequest = engine.Requests.Last(
            request => request.Uri == "cove://commands/dictation.stop");
        var final = JsonSerializer.Deserialize(
            stopRequest.Params!.Value,
            CoveJsonContext.Default.DictationStopParams)!;
        Assert.Equal("session-1", final.SessionId);
        Assert.Equal(16000, final.Audio.SampleRate);
        Assert.Equal(0, final.Audio.Offset);
        Assert.Equal(
            [0x00, 0x00, 0x00, 0x3f, 0x00, 0x00, 0x80, 0xbe],
            final.Audio.Pcm);
    }

    [Fact]
    public async Task RecorderFailure_CancelsEngineSessionAndPreservesStartErrorShape()
    {
        var recorder = new FakeRecorder
        {
            StartFailure = new InvalidOperationException(
                "microphone unavailable"),
        };
        var engine = new FakeDictationEngine();
        engine.Respond(
            "cove://commands/dictation.begin",
            """{"sessionId":"session-2"}""");
        engine.Respond(
            "cove://commands/dictation.cancel",
            "{}");
        await using var host = CreateHost(engine, recorder);

        var result = await host.StartDictation();

        Assert.Equal(
            """{"ok":false,"error":"microphone unavailable"}""",
            result);
        Assert.DoesNotContain(
            engine.Requests,
            request =>
                request.Uri == "cove://commands/dictation.started");
        var cancelRequest = Assert.Single(
            engine.Requests,
            request =>
                request.Uri == "cove://commands/dictation.cancel");
        var cancel = JsonSerializer.Deserialize(
            cancelRequest.Params!.Value,
            CoveJsonContext.Default.DictationSessionParams)!;
        Assert.Equal("session-2", cancel.SessionId);
    }

    [Fact]
    public async Task DisposeAsync_CancelsActiveCaptureAndDisposesRecorderOnce()
    {
        var recorder = new FakeRecorder();
        var engine = new FakeDictationEngine();
        engine.Respond(
            "cove://commands/dictation.begin",
            """{"sessionId":"session-3"}""");
        engine.Respond(
            "cove://commands/dictation.started",
            "{}");
        engine.Respond(
            "cove://commands/dictation.partial",
            "{}");
        engine.Respond(
            "cove://commands/dictation.cancel",
            "{}");
        var host = CreateHost(
            engine,
            recorder,
            TimeSpan.FromHours(1));
        Assert.Equal(
            """{"ok":true}""",
            await host.StartDictation());

        await host.DisposeAsync();
        await host.DisposeAsync();

        Assert.Equal(1, recorder.DisposeCount);
        var cancelRequest = Assert.Single(
            engine.Requests,
            request =>
                request.Uri == "cove://commands/dictation.cancel");
        var cancel = JsonSerializer.Deserialize(
            cancelRequest.Params!.Value,
            CoveJsonContext.Default.DictationSessionParams)!;
        Assert.Equal("session-3", cancel.SessionId);
    }

    private static DictationHost CreateHost(
        FakeDictationEngine engine,
        FakeRecorder recorder,
        TimeSpan? partialInterval = null) =>
        new(
            NullLogger<DictationHost>.Instance,
            engine.RequestAsync,
            () => recorder,
            partialInterval ?? TimeSpan.FromSeconds(1));

    private sealed class FakeDictationEngine
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, JsonElement> _responses = [];

        public List<RequestRecord> Requests { get; } = [];

        public TaskCompletionSource<RequestRecord> PartialReceived { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Respond(string uri, string json)
        {
            _responses[uri] =
                JsonDocument.Parse(json).RootElement.Clone();
        }

        public Task<ControlResponse> RequestAsync(
            string uri,
            JsonElement? parameters,
            CancellationToken cancellationToken)
        {
            RequestRecord request;
            JsonElement response;
            lock (_sync)
            {
                request = new RequestRecord(
                    uri,
                    parameters?.Clone());
                Requests.Add(request);
                Assert.True(
                    _responses.TryGetValue(uri, out response),
                    $"Unexpected dictation request: {uri}");
            }
            if (uri == "cove://commands/dictation.partial")
                PartialReceived.TrySetResult(request);
            return Task.FromResult(
                new ControlResponse("fake", true, response));
        }
    }

    private sealed record RequestRecord(
        string Uri,
        JsonElement? Params);

    private sealed class FakeRecorder : IAudioRecorder, IDisposable
    {
        public Exception? StartFailure { get; init; }

        public AudioSnapshot SnapshotResult { get; init; } =
            new([], 0);

        public float[] StopResult { get; init; } = [];

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public void Start()
        {
            StartCount++;
            if (StartFailure is not null)
                throw StartFailure;
        }

        public float[] Stop()
        {
            StopCount++;
            return StopResult;
        }

        public AudioSnapshot Snapshot(double maxSeconds)
        {
            Assert.Equal(
                PartialTranscriptTracker.SnapshotWindowSeconds,
                maxSeconds);
            return SnapshotResult;
        }

        public void Dispose() => DisposeCount++;
    }
}
