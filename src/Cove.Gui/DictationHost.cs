using System.Text.Json;
using Cove.Dictation;
using Microsoft.Extensions.Logging;
using Ryn.Core;

namespace Cove.Gui;

public sealed class DictationHost : IDisposable
{
    private readonly EngineLink _link;
    private readonly IRynWindowManager _windows;
    private readonly ILogger<DictationHost> _log;
    private readonly object _sync = new();
    private DictationService? _service;
    private PortAudioRecorder? _recorder;
    private SileroSpeechTrimmer? _trimmer;
    private SherpaTranscriber? _transcriber;
    private Task? _ensureTask;

    public DictationHost(EngineLink link, IRynWindowManager windows, ILogger<DictationHost> log)
    {
        _link = link;
        _windows = windows;
        _log = log;
    }

    private string ModelsRoot
    {
        get
        {
            var channel = _link.Channel switch
            {
                "beta" => Cove.Platform.CoveChannel.Beta,
                "dev" => Cove.Platform.CoveChannel.Dev,
                _ => Cove.Platform.CoveChannel.Stable,
            };
            return Path.Combine(Cove.Platform.CoveDataDir.Resolve(channel).Root, "models");
        }
    }

    public string Status()
    {
        var manager = new DictationModelManager(ModelsRoot, _log);
        var state = _service?.State ?? DictationState.Idle;
        return JsonSerializer.Serialize(
            new DictationStatusDto(state.ToString().ToLowerInvariant(), manager.TryGetModelDir() is not null),
            DictationJsonContext.Default.DictationStatusDto);
    }

    public string EnsureModel()
    {
        var manager = new DictationModelManager(ModelsRoot, _log);
        if (manager.TryGetModelDir() is not null)
            return """{"ready":true}""";
        lock (_sync)
        {
            if (_ensureTask is { IsCompleted: false })
                return """{"started":true}""";
            _ensureTask = Task.Run(async () =>
            {
                try
                {
                    var progress = new Progress<double>(p =>
                        Emit("dictation.progress", JsonSerializer.Serialize(new DictationProgressDto(p), DictationJsonContext.Default.DictationProgressDto)));
                    await manager.EnsureModelAsync(progress, CancellationToken.None).ConfigureAwait(false);
                    Emit("dictation.model", """{"ready":true}""");
                }
                catch (Exception ex)
                {
                    _log.DictationEnsureFailed(ex.Message);
                    Emit("dictation.model", JsonSerializer.Serialize(new DictationErrorDto(ex.Message), DictationJsonContext.Default.DictationErrorDto));
                }
            });
        }
        return """{"started":true}""";
    }

    public string StartDictation()
    {
        try
        {
            var service = GetOrCreateService();
            if (service is null)
                return """{"ok":false,"error":"model not downloaded"}""";
            var ok = service.Start();
            if (ok)
                Emit("dictation.state", """{"recording":true}""");
            return ok ? """{"ok":true}""" : """{"ok":false,"error":"busy"}""";
        }
        catch (DictationException ex)
        {
            return JsonSerializer.Serialize(new DictationStartErrorDto(false, ex.Message), DictationJsonContext.Default.DictationStartErrorDto);
        }
    }

    public async ValueTask<string> StopDictation()
    {
        DictationService? service;
        lock (_sync)
            service = _service;
        Emit("dictation.state", """{"recording":false}""");
        if (service is null)
            return """{"text":""}""";
        var result = await service.StopAsync().ConfigureAwait(false);
        return JsonSerializer.Serialize(
            new DictationResultDto(result.Text, result.AudioSeconds, result.TranscribeMs),
            DictationJsonContext.Default.DictationResultDto);
    }

    private DictationService? GetOrCreateService()
    {
        lock (_sync)
        {
            if (_service is not null)
                return _service;
            var manager = new DictationModelManager(ModelsRoot, _log);
            if (manager.TryGetModelDir() is not { } modelDir)
                return null;
            _recorder = new PortAudioRecorder();
            _trimmer = new SileroSpeechTrimmer(manager.VadPath);
            _transcriber = new SherpaTranscriber(modelDir);
            _service = new DictationService(_recorder, _trimmer, _transcriber, _log);
            return _service;
        }
    }

    private void Emit(string channel, string payloadJson)
    {
        var window = _windows.Windows.Count > 0 ? _windows.Windows[0] : null;
        if (window is not RynWindow rynWindow || rynWindow.WebView is not { } webView)
            return;
        using var doc = JsonDocument.Parse(payloadJson);
        var evt = new EngineEventPayload(channel, doc.RootElement.Clone());
        webView.EmitEvent("engine.event", JsonSerializer.Serialize(evt, EngineEventPayloadJsonContext.Default.EngineEventPayload));
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _recorder?.Dispose();
            _trimmer?.Dispose();
            _transcriber?.Dispose();
            _service = null;
        }
    }
}

public sealed record DictationStatusDto(string State, bool ModelReady);
public sealed record DictationProgressDto(double Percent);
public sealed record DictationErrorDto(string Error);
public sealed record DictationStartErrorDto(bool Ok, string Error);
public sealed record DictationResultDto(string Text, double AudioSeconds, long TranscribeMs);

[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
[System.Text.Json.Serialization.JsonSerializable(typeof(DictationStatusDto))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DictationProgressDto))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DictationErrorDto))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DictationStartErrorDto))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DictationResultDto))]
public sealed partial class DictationJsonContext : System.Text.Json.Serialization.JsonSerializerContext { }
