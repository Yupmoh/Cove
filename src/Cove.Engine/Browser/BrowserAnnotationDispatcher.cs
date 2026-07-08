using Microsoft.Extensions.Logging;

namespace Cove.Engine.Browser;

public sealed record AnnotationDispatchRequest(
    string TargetPaneId,
    string SourceUrl,
    string[] AnnotationIds,
    string? TaskRunId,
    string AdditionalContext);

public sealed record AnnotationDispatchResult(
    string DispatchId,
    string TargetPaneId,
    int AnnotationCount,
    System.DateTimeOffset DispatchedAt);

public sealed class BrowserAnnotationDispatcher
{
    private readonly BrowserStore _store;
    private readonly ILogger _logger;

    public BrowserAnnotationDispatcher(BrowserStore store, ILogger logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<AnnotationDispatchResult> DispatchAsync(AnnotationDispatchRequest request, Func<string, byte[], Task> writeToPane)
    {
        if (string.IsNullOrWhiteSpace(request.TargetPaneId))
        {
            _logger.LogWarning("browser-annotation-dispatch: no target pane specified");
            throw new ArgumentException("target pane required", nameof(request));
        }

        var dispatchId = System.Guid.NewGuid().ToString("N");
        var dispatchedAt = System.DateTimeOffset.UtcNow;
        var urlKey = BrowserStore.NormalizeUrlKey(request.SourceUrl);

        var allAnnotations = _store.ListAnnotations(urlKey);
        var annotations = new List<BrowserAnnotation>();
        foreach (var annId in request.AnnotationIds)
        {
            var match = allAnnotations.FirstOrDefault(a => a.Id == annId);
            if (match is not null)
                annotations.Add(match);
            else
                _logger.LogWarning("browser-annotation-dispatch: annotation {id} not found for url {url}", annId, request.SourceUrl);
        }

        if (annotations.Count == 0)
        {
            _logger.LogWarning("browser-annotation-dispatch: no annotations found for url {url}", request.SourceUrl);
            throw new InvalidOperationException("no annotations found to dispatch");
        }

        var renderedMessage = RenderMessage(request, annotations);
        var bytes = System.Text.Encoding.UTF8.GetBytes(renderedMessage + "\r");

        await writeToPane(request.TargetPaneId, bytes).ConfigureAwait(false);

        _logger.LogInformation("browser-annotation-dispatch: {id} → pane {pane} ({count} annotations from {url})",
            dispatchId, request.TargetPaneId, annotations.Count, request.SourceUrl);

        return new AnnotationDispatchResult(dispatchId, request.TargetPaneId, annotations.Count, dispatchedAt);
    }

    private static string RenderMessage(AnnotationDispatchRequest request, IReadOnlyList<BrowserAnnotation> annotations)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Browser Annotation Dispatch ===");
        sb.AppendLine($"From: {request.SourceUrl}");
        if (request.TaskRunId is not null)
            sb.AppendLine($"Task Run: {request.TaskRunId}");
        sb.AppendLine($"Annotations: {annotations.Count}");
        sb.AppendLine("---");
        foreach (var ann in annotations)
        {
            sb.AppendLine($"[{ann.Kind}] {ann.Text}");
            if (ann.AnchorJson is { Length: > 0 })
                sb.AppendLine($"  Anchor: {ann.AnchorJson}");
            sb.AppendLine($"  Source: {ann.Source}");
            sb.AppendLine($"  Resolved: {(ann.Resolved ? "yes" : "no")}");
        }
        if (!string.IsNullOrWhiteSpace(request.AdditionalContext))
        {
            sb.AppendLine("---");
            sb.AppendLine(request.AdditionalContext);
        }
        sb.Append("=== End Annotation Dispatch ===");
        return sb.ToString();
    }
}
