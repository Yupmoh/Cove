using System.Collections.Concurrent;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Browser;

public sealed record AutomationOutcome(bool Ok, string? ResultJson, string? ErrorCode, string? ErrorMessage)
{
    public static AutomationOutcome Success(string resultJson) => new(true, resultJson, null, null);
    public static AutomationOutcome Adrift() => new(false, null, "adrift", "no desktop app is attached to run this page — open the Cove GUI and retry");
}

public sealed class BrowserAutomationBridge
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();
    private readonly Action<BrowserAutomationExecEvent> _emit;
    private readonly ILogger _logger;
    private readonly TimeSpan _timeout;

    public BrowserAutomationBridge(Action<BrowserAutomationExecEvent> emit, ILogger logger, TimeSpan? timeout = null)
    {
        _emit = emit;
        _logger = logger;
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    public async Task<AutomationOutcome> ExecuteAsync(string paneId, string kind, string? refId, string? value, string? js, CancellationToken ct)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = tcs;
        try
        {
            _emit(new BrowserAutomationExecEvent(requestId, paneId, kind, refId, value, js));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeout);
            using var reg = cts.Token.Register(() => tcs.TrySetCanceled());
            var result = await tcs.Task.ConfigureAwait(false);
            return AutomationOutcome.Success(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("browser automation: no GUI answered {kind} for pane {paneId} within {timeout}s (request {requestId})", kind, paneId, _timeout.TotalSeconds, requestId);
            return AutomationOutcome.Adrift();
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    public bool Complete(string requestId, string resultJson)
    {
        if (_pending.TryRemove(requestId, out var tcs))
            return tcs.TrySetResult(resultJson);
        _logger.LogWarning("browser automation: result for unknown or expired request {requestId} dropped", requestId);
        return false;
    }

    public int PendingCount => _pending.Count;
}
