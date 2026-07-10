using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class SketchSvgSerializer
{
    private readonly ILogger _logger;

    public SketchSvgSerializer(ILogger logger)
    {
        _logger = logger;
    }

    public string Serialize(string sceneJson)
    {
        SketchScene? scene;
        try
        {
            scene = JsonSerializer.Deserialize(sceneJson, SketchSceneJsonContext.Default.SketchScene);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("sketch-svg: failed to parse scene JSON: {err}", ex.Message);
            scene = null;
        }

        if (scene is null || scene.Elements is null || scene.Elements.Count == 0)
            return EmptySvg();

        var bounds = ComputeBounds(scene.Elements);
        var padding = 20;
        var minX = bounds.MinX - padding;
        var minY = bounds.MinY - padding;
        var width = (bounds.MaxX - bounds.MinX) + padding * 2;
        var height = (bounds.MaxY - bounds.MinY) + padding * 2;

        var sb = new System.Text.StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width:F0}\" height=\"{height:F0}\" viewBox=\"{minX:F0} {minY:F0} {width:F0} {height:F0}\">");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#1a1a2e\"/>");

        foreach (var el in scene.Elements)
        {
            switch (el.Type)
            {
                case "line":
                    sb.Append($"<line x1=\"{el.X1:F1}\" y1=\"{el.Y1:F1}\" x2=\"{el.X2:F1}\" y2=\"{el.Y2:F1}\" stroke=\"#e5e9f0\" stroke-width=\"2\" stroke-linecap=\"round\"/>");
                    break;
                case "arrow":
                    sb.Append($"<line x1=\"{el.X1:F1}\" y1=\"{el.Y1:F1}\" x2=\"{el.X2:F1}\" y2=\"{el.Y2:F1}\" stroke=\"#e5e9f0\" stroke-width=\"2\" stroke-linecap=\"round\"/>");
                    var angle = System.Math.Atan2(el.Y2 - el.Y1, el.X2 - el.X1);
                    var headLen = 10.0;
                    var ax1 = el.X2 - headLen * System.Math.Cos(angle - System.Math.PI / 6);
                    var ay1 = el.Y2 - headLen * System.Math.Sin(angle - System.Math.PI / 6);
                    var ax2 = el.X2 - headLen * System.Math.Cos(angle + System.Math.PI / 6);
                    var ay2 = el.Y2 - headLen * System.Math.Sin(angle + System.Math.PI / 6);
                    sb.Append($"<polyline points=\"{ax1:F1},{ay1:F1} {el.X2:F1},{el.Y2:F1} {ax2:F1},{ay2:F1}\" fill=\"none\" stroke=\"#e5e9f0\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
                    break;
                case "rectangle":
                    var rx = System.Math.Min(el.X1, el.X2);
                    var ry = System.Math.Min(el.Y1, el.Y2);
                    var rw = System.Math.Abs(el.X2 - el.X1);
                    var rh = System.Math.Abs(el.Y2 - el.Y1);
                    sb.Append($"<rect x=\"{rx:F1}\" y=\"{ry:F1}\" width=\"{rw:F1}\" height=\"{rh:F1}\" fill=\"none\" stroke=\"#e5e9f0\" stroke-width=\"2\"/>");
                    break;
                case "ellipse":
                    var cx = (el.X1 + el.X2) / 2;
                    var cy = (el.Y1 + el.Y2) / 2;
                    var erx = System.Math.Abs(el.X2 - el.X1) / 2;
                    var ery = System.Math.Abs(el.Y2 - el.Y1) / 2;
                    sb.Append($"<ellipse cx=\"{cx:F1}\" cy=\"{cy:F1}\" rx=\"{erx:F1}\" ry=\"{ery:F1}\" fill=\"none\" stroke=\"#e5e9f0\" stroke-width=\"2\"/>");
                    break;
            }
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string EmptySvg()
    {
        return "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\" viewBox=\"0 0 100 100\"><rect width=\"100%\" height=\"100%\" fill=\"#1a1a2e\"/></svg>";
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) ComputeBounds(System.Collections.Generic.IReadOnlyList<SketchElement> elements)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var el in elements)
        {
            minX = System.Math.Min(minX, System.Math.Min(el.X1, el.X2));
            minY = System.Math.Min(minY, System.Math.Min(el.Y1, el.Y2));
            maxX = System.Math.Max(maxX, System.Math.Max(el.X1, el.X2));
            maxY = System.Math.Max(maxY, System.Math.Max(el.Y1, el.Y2));
        }
        if (minX == double.MaxValue) return (0, 0, 100, 100);
        return (minX, minY, maxX, maxY);
    }
}

public sealed record SketchScene
{
    private readonly System.Collections.Generic.IReadOnlyList<SketchElement>? _elements;
    public System.Collections.Generic.IReadOnlyList<SketchElement> Elements { get => _elements ?? []; init => _elements = value; }
    public SketchAppState? AppState { get; init; }
}

public sealed record SketchElement
{
    public string Type { get; init; } = "";
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
}

public sealed record SketchAppState
{
    public double Zoom { get; init; }
    public double ScrollX { get; init; }
    public double ScrollY { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SketchScene))]
public sealed partial class SketchSceneJsonContext : JsonSerializerContext { }
