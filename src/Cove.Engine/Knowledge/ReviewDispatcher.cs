using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed record ReviewDispatchRequest(
    string TargetPaneId,
    string WorkspaceId,
    string SessionId,
    string? TaskRunId,
    string Message,
    string? CommitSha);

public sealed record ReviewDispatchResult(
    string DispatchId,
    string TargetPaneId,
    string SessionId,
    string? TaskRunId,
    System.DateTimeOffset DispatchedAt);

public sealed class ReviewDispatcher
{
    private readonly ILogger _logger;

    public ReviewDispatcher(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<ReviewDispatchResult> DispatchAsync(ReviewDispatchRequest request, Func<string, byte[], Task> writeToPane)
    {
        var dispatchId = System.Guid.NewGuid().ToString("N");
        var dispatchedAt = System.DateTimeOffset.UtcNow;

        var renderedMessage = RenderMessage(request);
        var bytes = System.Text.Encoding.UTF8.GetBytes(renderedMessage + "\r");

        await writeToPane(request.TargetPaneId, bytes).ConfigureAwait(false);

        _logger.LogWarning("review-dispatch: {id} → pane {pane} (session {session}, task {task})",
            dispatchId, request.TargetPaneId, request.SessionId, request.TaskRunId ?? "none");

        return new ReviewDispatchResult(dispatchId, request.TargetPaneId, request.SessionId, request.TaskRunId, dispatchedAt);
    }

    private static string RenderMessage(ReviewDispatchRequest request)
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"=== Review Dispatch ===");
        lines.AppendLine($"From: review ({request.CommitSha ?? "current"})");
        lines.AppendLine($"Session: {request.SessionId}");
        if (request.TaskRunId is not null)
            lines.AppendLine($"Task Run: {request.TaskRunId}");
        lines.AppendLine($"---");
        lines.AppendLine(request.Message);
        lines.Append($"=== End Review ===");
        return lines.ToString();
    }
}
