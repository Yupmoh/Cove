using System.Text.Json;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Cove.Engine.Knowledge;

public sealed class SketchPngRasterizer
{
    private static readonly SKColor Background = new(0x1a, 0x1a, 0x2e);
    private static readonly SKColor Ink = new(0xe5, 0xe9, 0xf0);
    private const float StrokeWidth = 2f;
    private const double Padding = 20;

    private readonly ILogger _logger;

    public SketchPngRasterizer(ILogger logger)
    {
        _logger = logger;
    }

    public byte[] Rasterize(string sceneJson)
    {
        SketchScene? scene;
        try
        {
            scene = JsonSerializer.Deserialize(sceneJson, SketchSceneJsonContext.Default.SketchScene);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("sketch-png: failed to parse scene JSON: {err}", ex.Message);
            scene = null;
        }

        if (scene is null || scene.Elements is null || scene.Elements.Count == 0)
            return RenderEmpty();

        var (minX, minY, maxX, maxY) = ComputeBounds(scene.Elements);
        var originX = minX - Padding;
        var originY = minY - Padding;
        var width = (int)System.Math.Round((maxX - minX) + Padding * 2);
        var height = (int)System.Math.Round((maxY - minY) + Padding * 2);
        if (width <= 0 || height <= 0)
            return RenderEmpty();

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(Background);
        canvas.Translate((float)-originX, (float)-originY);

        using var paint = new SKPaint
        {
            Color = Ink,
            StrokeWidth = StrokeWidth,
            IsStroke = true,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };

        foreach (var el in scene.Elements)
            DrawElement(canvas, paint, el);

        return Encode(surface);
    }

    private static void DrawElement(SKCanvas canvas, SKPaint paint, SketchElement el)
    {
        switch (el.Type)
        {
            case "line":
                canvas.DrawLine((float)el.X1, (float)el.Y1, (float)el.X2, (float)el.Y2, paint);
                break;
            case "arrow":
                canvas.DrawLine((float)el.X1, (float)el.Y1, (float)el.X2, (float)el.Y2, paint);
                DrawArrowHead(canvas, paint, el);
                break;
            case "rectangle":
                var rect = SKRect.Create(
                    (float)System.Math.Min(el.X1, el.X2),
                    (float)System.Math.Min(el.Y1, el.Y2),
                    (float)System.Math.Abs(el.X2 - el.X1),
                    (float)System.Math.Abs(el.Y2 - el.Y1));
                canvas.DrawRect(rect, paint);
                break;
            case "ellipse":
                var oval = SKRect.Create(
                    (float)System.Math.Min(el.X1, el.X2),
                    (float)System.Math.Min(el.Y1, el.Y2),
                    (float)System.Math.Abs(el.X2 - el.X1),
                    (float)System.Math.Abs(el.Y2 - el.Y1));
                canvas.DrawOval(oval, paint);
                break;
        }
    }

    private static void DrawArrowHead(SKCanvas canvas, SKPaint paint, SketchElement el)
    {
        var angle = System.Math.Atan2(el.Y2 - el.Y1, el.X2 - el.X1);
        var headLen = 10.0;
        var ax1 = el.X2 - headLen * System.Math.Cos(angle - System.Math.PI / 6);
        var ay1 = el.Y2 - headLen * System.Math.Sin(angle - System.Math.PI / 6);
        var ax2 = el.X2 - headLen * System.Math.Cos(angle + System.Math.PI / 6);
        var ay2 = el.Y2 - headLen * System.Math.Sin(angle + System.Math.PI / 6);
        var builder = new SKPathBuilder();
        builder.MoveTo((float)ax1, (float)ay1);
        builder.LineTo((float)el.X2, (float)el.Y2);
        builder.LineTo((float)ax2, (float)ay2);
        using var path = builder.Detach();
        canvas.DrawPath(path, paint);
    }

    private static byte[] RenderEmpty()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(Background);
        return Encode(surface);
    }

    private static byte[] Encode(SKSurface surface)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
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
