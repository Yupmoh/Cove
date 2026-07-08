using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Browser;

public sealed record BrowserAnnotationAnchor(string Selector, int TextOffset, string SnapshotText, int ViewportTop, int ViewportLeft);

public sealed class BrowserAnnotationAnchorEngine
{
    private readonly ILogger _logger;

    public BrowserAnnotationAnchorEngine(ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public string SerializeAnchor(BrowserAnnotationAnchor anchor)
    {
        return JsonSerializer.Serialize(anchor, BrowserAnchorJsonContext.Default.BrowserAnnotationAnchor);
    }

    public BrowserAnnotationAnchor? DeserializeAnchor(string? anchorJson)
    {
        if (string.IsNullOrWhiteSpace(anchorJson))
        {
            _logger.LogWarning("browser-anchor: empty anchor json");
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize(anchorJson, BrowserAnchorJsonContext.Default.BrowserAnnotationAnchor);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "browser-anchor: failed to deserialize anchor json");
            return null;
        }
    }

    public BrowserAnnotationAnchor? ReAnchor(BrowserAnnotationAnchor? oldAnchor, int newViewportTop, int newViewportLeft)
    {
        if (oldAnchor is null)
        {
            _logger.LogWarning("browser-anchor: cannot re-anchor null anchor");
            return null;
        }

        var deltaTop = newViewportTop - oldAnchor.ViewportTop;
        var deltaLeft = newViewportLeft - oldAnchor.ViewportLeft;

        if (deltaTop == 0 && deltaLeft == 0)
            return oldAnchor;

        return oldAnchor with { ViewportTop = newViewportTop, ViewportLeft = newViewportLeft };
    }

    public bool IsAnchorVisible(BrowserAnnotationAnchor anchor, int viewportHeight, int viewportWidth)
    {
        if (viewportHeight <= 0 || viewportWidth <= 0)
        {
            _logger.LogWarning("browser-anchor: invalid viewport dimensions {h}x{w}", viewportHeight, viewportWidth);
            return false;
        }
        return anchor.ViewportTop >= 0 && anchor.ViewportTop < viewportHeight
            && anchor.ViewportLeft >= 0 && anchor.ViewportLeft < viewportWidth;
    }

    public double DistanceFromViewport(BrowserAnnotationAnchor anchor, int viewportTop, int viewportHeight)
    {
        var anchorBottom = anchor.ViewportTop;
        var viewportBottom = viewportTop + viewportHeight;

        if (anchorBottom < viewportTop)
            return viewportTop - anchorBottom;
        if (anchorBottom > viewportBottom)
            return anchorBottom - viewportBottom;
        return 0;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BrowserAnnotationAnchor))]
public sealed partial class BrowserAnchorJsonContext : JsonSerializerContext { }
