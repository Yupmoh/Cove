using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SketchPngRasterizerTests
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static string SceneJson(params string[] elements)
    {
        return "{\"elements\":[" + string.Join(',', elements) + "]}";
    }

    private static string Line(double x1, double y1, double x2, double y2)
    {
        return $"{{\"type\":\"line\",\"x1\":{x1},\"y1\":{y1},\"x2\":{x2},\"y2\":{y2}}}";
    }

    [Fact]
    public void Rasterize_LineScene_ProducesPngWithExpectedDimensions()
    {
        var rasterizer = new SketchPngRasterizer(NullLogger.Instance);

        var png = rasterizer.Rasterize(SceneJson(Line(0, 0, 100, 50)));

        Assert.Equal(PngSignature, png.Take(8).ToArray());
        using var bitmap = SKBitmap.Decode(png);
        Assert.Equal(140, bitmap.Width);
        Assert.Equal(90, bitmap.Height);
    }

    [Fact]
    public void Rasterize_LineScene_DrawsInkOverBackground()
    {
        var rasterizer = new SketchPngRasterizer(NullLogger.Instance);

        var png = rasterizer.Rasterize(SceneJson(Line(0, 0, 100, 100)));

        using var bitmap = SKBitmap.Decode(png);
        var center = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
        Assert.True(center.Red > 100 && center.Green > 100 && center.Blue > 100, $"expected light ink at center, got {center}");
        var corner = bitmap.GetPixel(2, 2);
        Assert.True(corner.Red < 80 && corner.Blue < 80, $"expected dark background at corner, got {corner}");
    }

    [Fact]
    public void Rasterize_EmptyScene_ProducesFallbackPng()
    {
        var rasterizer = new SketchPngRasterizer(NullLogger.Instance);

        var png = rasterizer.Rasterize("{\"elements\":[]}");

        using var bitmap = SKBitmap.Decode(png);
        Assert.Equal(100, bitmap.Width);
        Assert.Equal(100, bitmap.Height);
    }

    [Fact]
    public void Rasterize_InvalidJson_ProducesFallbackPng()
    {
        var rasterizer = new SketchPngRasterizer(NullLogger.Instance);

        var png = rasterizer.Rasterize("not json");

        Assert.Equal(PngSignature, png.Take(8).ToArray());
        using var bitmap = SKBitmap.Decode(png);
        Assert.Equal(100, bitmap.Width);
    }

    [Fact]
    public void Rasterize_AllElementTypes_Succeeds()
    {
        var rasterizer = new SketchPngRasterizer(NullLogger.Instance);
        var scene = SceneJson(
            Line(0, 0, 50, 50),
            "{\"type\":\"arrow\",\"x1\":10,\"y1\":10,\"x2\":90,\"y2\":10}",
            "{\"type\":\"rectangle\",\"x1\":20,\"y1\":20,\"x2\":80,\"y2\":60}",
            "{\"type\":\"ellipse\",\"x1\":30,\"y1\":30,\"x2\":70,\"y2\":70}");

        var png = rasterizer.Rasterize(scene);

        Assert.Equal(PngSignature, png.Take(8).ToArray());
        using var bitmap = SKBitmap.Decode(png);
        Assert.True(bitmap.Width > 0 && bitmap.Height > 0);
    }
}
