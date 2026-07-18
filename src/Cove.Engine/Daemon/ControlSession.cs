using System.Text;
using System.Text.Json;
using Cove.Engine.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Daemon;

internal sealed class ControlSession
{
    private readonly DaemonPaths _paths;
    private readonly EngineRuntime _runtime;
    private readonly HandoffTransport _handoff;
    private readonly DaemonCommandSagas _sagas;
    private readonly string _controlToken;
    private readonly ILogger _logger;
    private readonly Action _requestShutdown;
    private readonly Func<int> _totalConnections;
    private bool _helloDone;
    private bool _isGui;
    private string? _principalNookId;

    public ControlSession(
        DaemonPaths paths,
        EngineRuntime runtime,
        HandoffTransport handoff,
        DaemonCommandSagas sagas,
        string controlToken,
        ILogger logger,
        Action requestShutdown,
        Func<int> totalConnections)
    {
        _paths = paths;
        _runtime = runtime;
        _handoff = handoff;
        _sagas = sagas;
        _controlToken = controlToken;
        _logger = logger;
        _requestShutdown = requestShutdown;
        _totalConnections = totalConnections;
    }

    public async Task RunAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var connection = new FrameConnection(stream);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var maybe = await connection.ReadFrameAsync(
                    cancellationToken).ConfigureAwait(false);
                if (maybe is null)
                    break;
                var frame = maybe.Value;
                if (frame.Header.Type == FrameType.Credit)
                    continue;
                if (frame.Header.Type != FrameType.Request)
                {
                    await WriteErrorFrameAsync(
                        connection,
                        "malformed_frame",
                        "control connection expects Request frames",
                        null,
                        cancellationToken).ConfigureAwait(false);
                    break;
                }
                var request = ControlCodec.DecodeRequest(frame.Payload);
                var stop = await DispatchAsync(
                    connection,
                    stream,
                    request,
                    cancellationToken).ConfigureAwait(false);
                if (stop)
                    break;
            }
        }
        catch (ProtocolException ex)
        {
            try
            {
                await WriteErrorFrameAsync(
                    connection,
                    ex.Code,
                    ex.Message,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception writeError)
            {
                _logger.ControlErrorWriteFailed(writeError.Message);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DaemonLog.Write(
                _paths,
                "connection error: " + ex.Message);
        }
        finally
        {
            if (_isGui)
                _runtime.Events.UnregisterGui(connection);
            try
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ControlSessionDisposeFailed(ex.Message);
            }
        }
    }

    private async Task<bool> DispatchAsync(
        FrameConnection connection,
        Stream stream,
        ControlRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Uri == "cove://sys/hello")
        {
            return await HandleHelloAsync(
                connection,
                request,
                cancellationToken).ConfigureAwait(false);
        }

        if (request.Uri == "cove://handoff/begin")
        {
            if (!_helloDone)
            {
                await WriteResponseAsync(
                    connection,
                    Fail(
                        request.Id,
                        "not_ready",
                        "sys/hello required before handoff"),
                    cancellationToken).ConfigureAwait(false);
                return false;
            }
            await WriteResponseAsync(
                connection,
                _handoff.Begin(request.Id),
                cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (!_helloDone)
        {
            await WriteResponseAsync(
                connection,
                Fail(
                    request.Id,
                    "not_ready",
                    "sys/hello required before commands"),
                cancellationToken).ConfigureAwait(false);
            return false;
        }

        request = SanitizeCallerIdentity(request);
        if (request.Uri == "cove://commands/nook.scope.set"
            && !_isGui)
        {
            _logger.ScopeMutationDenied(_principalNookId ?? "");
            await WriteResponseAsync(
                connection,
                Fail(
                    request.Id,
                    "access_denied",
                    "scope mutation requires the gui client"),
                cancellationToken).ConfigureAwait(false);
            return false;
        }

        var dispatchStart =
            System.Diagnostics.Stopwatch.GetTimestamp();
        var generated = await _runtime.RouteAsync(
            request,
            _sagas,
            cancellationToken).ConfigureAwait(false);
        if (generated is not null)
        {
            var dispatchMilliseconds =
                System.Diagnostics.Stopwatch
                    .GetElapsedTime(dispatchStart)
                    .TotalMilliseconds;
            _logger.ControlDispatch(
                request.Uri,
                dispatchMilliseconds,
                generated.Ok);
            if (!generated.Ok)
            {
                _logger.ControlDispatchFailed(
                    request.Uri,
                    generated.Error?.Code ?? "",
                    generated.Error?.Message ?? "");
            }
            if (generated.Ok)
                _runtime.Events.PublishMutation(request.Uri);
            await WriteResponseAsync(
                connection,
                generated,
                cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (request.Uri.StartsWith(
                "cove://state/",
                StringComparison.Ordinal))
        {
            await HandleStateUriAsync(
                connection,
                request,
                cancellationToken).ConfigureAwait(false);
            return false;
        }

        switch (request.Uri)
        {
            case "cove://sys/ping":
                await WriteResponseAsync(
                    connection,
                    new ControlResponse(
                        request.Id,
                        true,
                        Parse("{\"pong\":true}")),
                    cancellationToken).ConfigureAwait(false);
                return false;

            case "cove://sys/daemon.status":
                var status = new DaemonStatusResult(
                    Environment.ProcessId,
                    _runtime.Channel,
                    _runtime.EngineVersion,
                    _totalConnections(),
                    0,
                    (long)(DateTimeOffset.UtcNow
                        - _runtime.StartedAtUtc).TotalSeconds);
                await WriteResponseAsync(
                    connection,
                    new ControlResponse(
                        request.Id,
                        true,
                        JsonSerializer.SerializeToElement(
                            status,
                            CoveJsonContext.Default.DaemonStatusResult)),
                    cancellationToken).ConfigureAwait(false);
                return false;

            case "cove://sys/daemon.stop":
                await WriteResponseAsync(
                    connection,
                    new ControlResponse(
                        request.Id,
                        true,
                        Parse("{\"stopping\":true}")),
                    cancellationToken).ConfigureAwait(false);
                _requestShutdown();
                return true;

            case "cove://commands/window.focus":
                var focused = _runtime.Events.TryForwardFocus(
                    cancellationToken);
                if (focused
                    && _runtime.Bays.Registry.FocusedBayId
                    is { } focusedBay)
                {
                    _ = _runtime.Bays.RefreshWorktreesAsync(
                        focusedBay);
                }
                var focusResult = focused
                    ? Parse("{\"focused\":true}")
                    : Parse(
                        "{\"focused\":false,\"reason\":\"no_render_client\"}");
                await WriteResponseAsync(
                    connection,
                    new ControlResponse(
                        request.Id,
                        true,
                        focusResult),
                    cancellationToken).ConfigureAwait(false);
                return false;

            case "cove://commands/restore.summary.get":
                var summary = _runtime.Events.RestorationSummary;
                var pullResult =
                    new Cove.Engine.Restart.RestoreSummaryPullResult(
                        summary is not null,
                        summary?.Restored ?? 0,
                        summary?.Fresh ?? 0,
                        summary?.Skipped ?? 0,
                        summary?.BootedAt
                            ?? _runtime.StartedAtUtc.ToString("o"));
                await WriteResponseAsync(
                    connection,
                    new ControlResponse(
                        request.Id,
                        true,
                        JsonSerializer.SerializeToElement(
                            pullResult,
                            Cove.Engine.Restart
                                .RestorationSummaryJsonContext
                                .Default
                                .RestoreSummaryPullResult)),
                    cancellationToken).ConfigureAwait(false);
                return false;

            case "cove://commands/nook.subscribe":
                await _runtime.Streams.StreamAsync(
                    connection,
                    stream,
                    request,
                    cancellationToken).ConfigureAwait(false);
                return true;

            default:
                _logger.ControlDispatchFailed(
                    request.Uri,
                    "not_found",
                    "unknown command");
                await WriteResponseAsync(
                    connection,
                    Fail(
                        request.Id,
                        "not_found",
                        $"unknown command {request.Uri}"),
                    cancellationToken).ConfigureAwait(false);
                return false;
        }
    }

    private async Task<bool> HandleHelloAsync(
        FrameConnection connection,
        ControlRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Params is not JsonElement helloElement)
        {
            await WriteResponseAsync(
                connection,
                Fail(
                    request.Id,
                    "invalid_params",
                    "hello params required"),
                cancellationToken).ConfigureAwait(false);
            return false;
        }
        var parameters = helloElement.Deserialize(
            CoveJsonContext.Default.HelloParams);
        if (parameters is null)
        {
            await WriteResponseAsync(
                connection,
                Fail(
                    request.Id,
                    "invalid_params",
                    "hello params malformed"),
                cancellationToken).ConfigureAwait(false);
            return false;
        }
        if (parameters.ProtocolVersion
            != ProtocolConstants.SemanticProtocolVersion)
        {
            await WriteErrorFrameAsync(
                connection,
                "version_mismatch",
                $"protocol {parameters.ProtocolVersion} unsupported",
                null,
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        if (parameters.ClientKind == "gui"
            && !ControlTokenMatches(parameters.ControlToken))
        {
            _logger.GuiControlAuthRejected(parameters.ClientKind);
            await WriteResponseAsync(
                connection,
                Fail(
                    request.Id,
                    "control_auth_failed",
                    "gui client requires the daemon control token"),
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        if (!string.IsNullOrEmpty(parameters.NookId))
        {
            var authentication = _runtime.Nooks.Authenticate(
                parameters.NookId,
                parameters.NookToken);
            if (authentication == NookAuthResult.Rejected)
            {
                _logger.NookAuthRejected(parameters.NookId);
                await WriteResponseAsync(
                    connection,
                    Fail(
                        request.Id,
                        "nook_auth_failed",
                        $"nook credential rejected for {parameters.NookId}"),
                    cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (authentication
                is NookAuthResult.Bound
                or NookAuthResult.LegacyBound)
            {
                _principalNookId = parameters.NookId;
                if (authentication
                    == NookAuthResult.LegacyBound)
                {
                    _logger.NookAuthLegacyBound(
                        parameters.NookId);
                }
            }
            else
            {
                _logger.NookAuthUnknown(parameters.NookId);
            }
        }
        _helloDone = true;
        if (parameters.ClientKind == "gui")
        {
            _isGui = true;
            _runtime.Events.RegisterGui(connection);
        }
        var result = new HelloResult(
            ProtocolConstants.SemanticProtocolVersion,
            _runtime.EngineVersion,
            Environment.ProcessId,
            _runtime.Channel);
        await WriteResponseAsync(
            connection,
            new ControlResponse(
                request.Id,
                true,
                JsonSerializer.SerializeToElement(
                    result,
                    CoveJsonContext.Default.HelloResult)),
            cancellationToken).ConfigureAwait(false);
        return false;
    }

    private ControlRequest SanitizeCallerIdentity(
        ControlRequest request)
    {
        if (_principalNookId is { } principal)
        {
            if (string.Equals(
                    request.CallerNookId,
                    principal,
                    StringComparison.Ordinal))
            {
                return request;
            }
            if (!string.IsNullOrEmpty(request.CallerNookId))
            {
                _logger.CallerClaimOverridden(
                    request.CallerNookId,
                    principal,
                    request.Uri);
            }
            return request with { CallerNookId = principal };
        }
        if (!_isGui
            && !string.IsNullOrEmpty(request.CallerNookId))
        {
            _logger.CallerClaimStripped(
                request.CallerNookId,
                request.Uri);
            return request with { CallerNookId = null };
        }
        return request;
    }

    private async Task HandleStateUriAsync(
        FrameConnection connection,
        ControlRequest request,
        CancellationToken cancellationToken)
    {
        var path = request.Uri["cove://state/".Length..];
        var parts = path.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            await WriteResponseAsync(
                connection,
                Fail(
                    request.Id,
                    "invalid_params",
                    "cove://state/<scope>/<namespace>[/<id>] required"),
                cancellationToken).ConfigureAwait(false);
            return;
        }
        var scope = parts[0];
        var stateNamespace = parts[1];
        var id = parts.Length > 2 ? parts[2] : "default";
        if (!Cove.Engine.Protocol.StateBus.IsValidScope(scope))
        {
            await WriteResponseAsync(
                connection,
                Fail(
                    request.Id,
                    "invalid_params",
                    "scope must be app, bay, tab, or nook"),
                cancellationToken).ConfigureAwait(false);
            return;
        }
        var valueProperty = default(JsonElement);
        var isWrite = request.Params is JsonElement writeElement
            && writeElement.TryGetProperty(
                "value",
                out valueProperty);
        if (isWrite)
        {
            var rawValue =
                valueProperty.ValueKind == JsonValueKind.Null
                    ? null
                    : valueProperty.ValueKind
                        == JsonValueKind.String
                        ? valueProperty.GetString()
                        : valueProperty.GetRawText();
            _runtime.StateBus.Write(
                scope,
                stateNamespace,
                id,
                rawValue);
            await WriteResponseAsync(
                connection,
                new ControlResponse(
                    request.Id,
                    true,
                    Parse("{\"ok\":true}")),
                cancellationToken).ConfigureAwait(false);
            return;
        }
        var (exists, value) = _runtime.StateBus.Read(
            scope,
            stateNamespace,
            id);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("exists", exists);
            if (exists && value is not null)
                writer.WriteString("value", value);
            else
                writer.WriteNull("value");
            writer.WriteEndObject();
            writer.Flush();
        }
        using var document = JsonDocument.Parse(
            buffer.ToArray());
        await WriteResponseAsync(
            connection,
            new ControlResponse(
                request.Id,
                true,
                document.RootElement.Clone()),
            cancellationToken).ConfigureAwait(false);
    }

    private bool ControlTokenMatches(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate))
            return false;
        return System.Security.Cryptography
            .CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(_controlToken),
                Encoding.ASCII.GetBytes(candidate));
    }

    private static ControlResponse Fail(
        string id,
        string code,
        string message)
    {
        return new ControlResponse(
            id,
            false,
            null,
            new ControlError(code, message));
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static ValueTask WriteResponseAsync(
        FrameConnection connection,
        ControlResponse response,
        CancellationToken cancellationToken)
    {
        return connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(response),
            cancellationToken);
    }

    private static ValueTask WriteErrorFrameAsync(
        FrameConnection connection,
        string code,
        string message,
        ulong? streamId,
        CancellationToken cancellationToken)
    {
        return connection.WriteFrameAsync(
            FrameType.Error,
            0,
            ControlCodec.Encode(
                new ControlErrorFrame(
                    code,
                    message,
                    streamId)),
            cancellationToken);
    }
}
