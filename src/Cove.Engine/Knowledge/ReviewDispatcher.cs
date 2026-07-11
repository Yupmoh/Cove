using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed record ReviewDispatchRequest(
    string TargetNookId,
    string BayId,
    string SessionId,
    string? TaskRunId,
    string Message,
    string? CommitSha);

public sealed record ReviewDispatchResult(
    string DispatchId,
    string TargetNookId,
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

    public async Task<ReviewDispatchResult> DispatchAsync(ReviewDispatchRequest request, Func<string, byte[], Task> writeToNook)
    {
        var dispatchId = System.Guid.NewGuid().ToString("N");
        var dispatchedAt = System.DateTimeOffset.UtcNow;

        var renderedMessage = RenderMessage(request);
        var bytes = System.Text.Encoding.UTF8.GetBytes(renderedMessage + "\r");

        await writeToNook(request.TargetNookId, bytes).ConfigureAwait(false);

        _logger.LogWarning("review-dispatch: {id} → nook {nook} (session {session}, task {task})",
            dispatchId, request.TargetNookId, request.SessionId, request.TaskRunId ?? "none");

        return new ReviewDispatchResult(dispatchId, request.TargetNookId, request.SessionId, request.TaskRunId, dispatchedAt);
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
