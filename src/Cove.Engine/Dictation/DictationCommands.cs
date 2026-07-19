using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Dictation;

public static class DictationCommands
{
    [CoveCommand("cove://commands/dictation.status")]
    public static Task<ControlResponse> Status(EngineDispatchContext context)
    {
        if (context.Dictation is not { } dictation)
            return Task.FromResult(context.Fail("not_ready", "dictation runtime not available"));
        return Task.FromResult(
            context.Ok(
                dictation.Status(),
                CoveJsonContext.Default.DictationStatusResult));
    }

    [CoveCommand("cove://commands/dictation.ensure-model")]
    public static Task<ControlResponse> EnsureModel(EngineDispatchContext context)
    {
        if (context.Dictation is not { } dictation)
            return Task.FromResult(context.Fail("not_ready", "dictation runtime not available"));
        if (dictation.EnsureModel())
        {
            return Task.FromResult(
                context.Ok(
                    new DictationModelReadyResult(true),
                    CoveJsonContext.Default.DictationModelReadyResult));
        }
        return Task.FromResult(
            context.Ok(
                new DictationEnsureModelResult(true),
                CoveJsonContext.Default.DictationEnsureModelResult));
    }

    [CoveCommand("cove://commands/dictation.begin")]
    public static Task<ControlResponse> Begin(EngineDispatchContext context)
    {
        if (context.Dictation is not { } dictation)
            return Task.FromResult(context.Fail("not_ready", "dictation runtime not available"));
        var sessionId = dictation.Begin();
        if (sessionId is null)
            return Task.FromResult(context.Fail("not_ready", "dictation model not downloaded"));
        return Task.FromResult(
            context.Ok(
                new DictationBeginResult(sessionId),
                CoveJsonContext.Default.DictationBeginResult));
    }

    [CoveCommand("cove://commands/dictation.started")]
    public static Task<ControlResponse> Started(EngineDispatchContext context)
    {
        if (context.Dictation is not { } dictation)
            return Task.FromResult(context.Fail("not_ready", "dictation runtime not available"));
        if (!TrySession(context, out var sessionId)
            || !dictation.Started(sessionId))
        {
            return Task.FromResult(context.Fail("invalid_params", "valid sessionId required"));
        }
        return Task.FromResult(context.OkJson("{}"));
    }

    [CoveCommand("cove://commands/dictation.partial")]
    public static async Task<ControlResponse> Partial(EngineDispatchContext context)
    {
        if (context.Dictation is not { } dictation)
            return context.Fail("not_ready", "dictation runtime not available");
        if (context.Request.Params is not JsonElement element
            || element.Deserialize(CoveJsonContext.Default.DictationPartialParams) is not { } parameters
            || string.IsNullOrWhiteSpace(parameters.SessionId)
            || !await dictation.PartialAsync(
                    parameters.SessionId,
                    parameters.Audio,
                    context.CancellationToken)
                .ConfigureAwait(false))
        {
            return context.Fail("invalid_params", "valid session and audio required");
        }
        return context.OkJson("{}");
    }

    [CoveCommand("cove://commands/dictation.stop")]
    public static async Task<ControlResponse> Stop(EngineDispatchContext context)
    {
        if (context.Dictation is not { } dictation)
            return context.Fail("not_ready", "dictation runtime not available");
        if (context.Request.Params is not JsonElement element
            || element.Deserialize(CoveJsonContext.Default.DictationStopParams) is not { } parameters
            || string.IsNullOrWhiteSpace(parameters.SessionId))
        {
            return context.Fail("invalid_params", "valid session and audio required");
        }
        var result = await dictation
            .StopAsync(
                parameters.SessionId,
                parameters.Audio,
                context.CancellationToken)
            .ConfigureAwait(false);
        return result is null
            ? context.Fail("invalid_params", "valid session and audio required")
            : context.Ok(
                result,
                CoveJsonContext.Default.DictationTranscriptionResult);
    }

    [CoveCommand("cove://commands/dictation.cancel")]
    public static Task<ControlResponse> Cancel(EngineDispatchContext context)
    {
        if (context.Dictation is not { } dictation)
            return Task.FromResult(context.Fail("not_ready", "dictation runtime not available"));
        if (!TrySession(context, out var sessionId))
            return Task.FromResult(context.Fail("invalid_params", "sessionId required"));
        dictation.Cancel(sessionId);
        return Task.FromResult(context.OkJson("{}"));
    }

    private static bool TrySession(
        EngineDispatchContext context,
        out string sessionId)
    {
        sessionId = "";
        if (context.Request.Params is not JsonElement element
            || element.Deserialize(CoveJsonContext.Default.DictationSessionParams) is not { } parameters
            || string.IsNullOrWhiteSpace(parameters.SessionId))
        {
            return false;
        }
        sessionId = parameters.SessionId;
        return true;
    }
}
