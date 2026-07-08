using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Browser;

public sealed record BrowserAnnotationAnchor(string Selector, int TextOffset, string SnapshotText, int ElementTop, int ElementLeft);

public sealed record ViewportPosition(int Top, int Left);

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

    public ViewportPosition ComputeViewportPosition(BrowserAnnotationAnchor anchor, int scrollY, int scrollX)
    {
        return new ViewportPosition(anchor.ElementTop - scrollY, anchor.ElementLeft - scrollX);
    }

    public bool IsAnchorVisible(BrowserAnnotationAnchor anchor, int scrollY, int scrollX, int viewportHeight, int viewportWidth)
    {
        if (viewportHeight <= 0 || viewportWidth <= 0)
        {
            _logger.LogWarning("browser-anchor: invalid viewport dimensions {h}x{w}", viewportHeight, viewportWidth);
            return false;
        }
        var pos = ComputeViewportPosition(anchor, scrollY, scrollX);
        return pos.Top >= 0 && pos.Top < viewportHeight
            && pos.Left >= 0 && pos.Left < viewportWidth;
    }

    public double DistanceFromViewport(BrowserAnnotationAnchor anchor, int scrollY, int viewportHeight)
    {
        if (viewportHeight <= 0)
        {
            _logger.LogWarning("browser-anchor: invalid viewport height {h}", viewportHeight);
            return double.MaxValue;
        }
        var viewportTop = anchor.ElementTop - scrollY;
        var viewportBottom = viewportTop;
        var visibleTop = 0;
        var visibleBottom = viewportHeight;

        if (viewportBottom < visibleTop)
            return visibleTop - viewportBottom;
        if (viewportTop > visibleBottom)
            return viewportTop - visibleBottom;
        return 0;
    }

    public bool VerifyAnchorText(BrowserAnnotationAnchor anchor, string currentElementText)
    {
        if (string.IsNullOrEmpty(anchor.SnapshotText))
            return true;
        if (currentElementText.Length < anchor.TextOffset + anchor.SnapshotText.Length)
        {
            _logger.LogWarning("browser-anchor: element text shorter than anchor offset+snapshot");
            return false;
        }
        var slice = currentElementText.Substring(anchor.TextOffset, anchor.SnapshotText.Length);
        return slice == anchor.SnapshotText;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BrowserAnnotationAnchor))]
public sealed partial class BrowserAnchorJsonContext : JsonSerializerContext { }
