using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SketchSvgSerializerTests
{
    private readonly SketchSvgSerializer _serializer = new(NullLogger.Instance);

    [Fact]
    public void Serialize_LineElement_ProducesValidSvg()
    {
        var scene = """{"elements":[{"type":"line","x1":10,"y1":20,"x2":100,"y2":80}],"appState":{"zoom":1,"scrollX":0,"scrollY":0}}""";
        var svg = _serializer.Serialize(scene);

        Assert.StartsWith("<svg", svg);
        Assert.EndsWith("</svg>", svg);
        Assert.Contains("xmlns=\"http://www.w3.org/2000/svg\"", svg);
        Assert.Contains("<line", svg);
        Assert.Contains("x1=\"10", svg);
        Assert.Contains("y1=\"20", svg);
        Assert.Contains("x2=\"100", svg);
        Assert.Contains("y2=\"80", svg);
    }

    [Fact]
    public void Serialize_ArrowElement_IncludesArrowhead()
    {
        var scene = """{"elements":[{"type":"arrow","x1":0,"y1":0,"x2":50,"y2":50}],"appState":{"zoom":1,"scrollX":0,"scrollY":0}}""";
        var svg = _serializer.Serialize(scene);

        Assert.Contains("<line", svg);
        Assert.Contains("<polyline", svg);
    }

    [Fact]
    public void Serialize_RectangleElement_ProducesRect()
    {
        var scene = """{"elements":[{"type":"rectangle","x1":10,"y1":10,"x2":110,"y2":80}],"appState":{"zoom":1,"scrollX":0,"scrollY":0}}""";
        var svg = _serializer.Serialize(scene);

        Assert.Contains("<rect", svg);
        Assert.Contains("width=\"100", svg);
        Assert.Contains("height=\"70", svg);
    }

    [Fact]
    public void Serialize_EllipseElement_ProducesEllipse()
    {
        var scene = """{"elements":[{"type":"ellipse","x1":0,"y1":0,"x2":100,"y2":60}],"appState":{"zoom":1,"scrollX":0,"scrollY":0}}""";
        var svg = _serializer.Serialize(scene);

        Assert.Contains("<ellipse", svg);
        Assert.Contains("rx=\"50", svg);
        Assert.Contains("ry=\"30", svg);
    }

    [Fact]
    public void Serialize_EmptyScene_ProducesEmptySvg()
    {
        var scene = """{"elements":[],"appState":{"zoom":1,"scrollX":0,"scrollY":0}}""";
        var svg = _serializer.Serialize(scene);

        Assert.StartsWith("<svg", svg);
        Assert.EndsWith("</svg>", svg);
    }

    [Fact]
    public void Serialize_InvalidJson_ProducesEmptySvg()
    {
        var svg = _serializer.Serialize("not valid json");
        Assert.StartsWith("<svg", svg);
        Assert.EndsWith("</svg>", svg);
    }

    [Fact]
    public void Serialize_MultipleElements_AllRendered()
    {
        var scene = """{"elements":[{"type":"line","x1":0,"y1":0,"x2":50,"y2":50},{"type":"rectangle","x1":10,"y1":10,"x2":60,"y2":60},{"type":"ellipse","x1":20,"y1":20,"x2":80,"y2":60}],"appState":{"zoom":1,"scrollX":0,"scrollY":0}}""";
        var svg = _serializer.Serialize(scene);

        Assert.Contains("<line", svg);
        Assert.Contains("<rect", svg);
        Assert.Contains("<ellipse", svg);
    }
}
