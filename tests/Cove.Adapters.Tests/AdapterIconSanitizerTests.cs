using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class AdapterIconSanitizerTests
{
    [Fact]
    public void Sanitize_Null_ReturnsNull()
    {
        Assert.Null(AdapterIconSanitizer.Sanitize(null));
        Assert.Null(AdapterIconSanitizer.Sanitize("   "));
    }

    [Fact]
    public void Sanitize_NonSvg_ReturnsNull()
    {
        Assert.Null(AdapterIconSanitizer.Sanitize("<div>nope</div>"));
    }

    [Fact]
    public void Sanitize_KeepsPlainPath()
    {
        var svg = "<svg viewBox=\"0 0 24 24\"><path d=\"M1 1\" fill=\"currentColor\"/></svg>";
        var result = AdapterIconSanitizer.Sanitize(svg);
        Assert.NotNull(result);
        Assert.Contains("<path", result);
        Assert.Contains("currentColor", result);
    }

    [Fact]
    public void Sanitize_RejectsScript()
    {
        var svg = "<svg><script>alert(1)</script><path/></svg>";
        Assert.Null(AdapterIconSanitizer.Sanitize(svg));
    }

    [Fact]
    public void Sanitize_RejectsForeignObject()
    {
        var svg = "<svg><foreignObject><body/></foreignObject></svg>";
        Assert.Null(AdapterIconSanitizer.Sanitize(svg));
    }

    [Fact]
    public void Sanitize_StripsOnHandlers()
    {
        var svg = "<svg onload=\"evil()\"><path onclick='x()' d=\"M1 1\"/></svg>";
        var result = AdapterIconSanitizer.Sanitize(svg);
        Assert.NotNull(result);
        Assert.DoesNotContain("onload", result);
        Assert.DoesNotContain("onclick", result);
    }

    [Fact]
    public void Sanitize_StripsExternalHref()
    {
        var svg = "<svg><use xlink:href=\"http://evil.example/x.svg#a\"/><path/></svg>";
        var result = AdapterIconSanitizer.Sanitize(svg);
        Assert.NotNull(result);
        Assert.DoesNotContain("evil.example", result);
    }

    [Fact]
    public void Sanitize_StripsRootWidthHeightButKeepsInner()
    {
        var svg = "<svg width=\"512\" height=\"512\" viewBox=\"0 0 24 24\"><rect width=\"4\" height=\"4\"/></svg>";
        var result = AdapterIconSanitizer.Sanitize(svg);
        Assert.NotNull(result);
        var open = result!.Substring(0, result.IndexOf('>') + 1);
        Assert.DoesNotContain("width", open);
        Assert.DoesNotContain("height", open);
        Assert.Contains("<rect width=\"4\" height=\"4\"", result);
    }

    [Fact]
    public void Sanitize_RejectsOversize()
    {
        var big = "<svg>" + new string('a', 70 * 1024) + "</svg>";
        Assert.Null(AdapterIconSanitizer.Sanitize(big));
    }
}

public sealed class RetentionThresholdTests
{
    [Fact]
    public void Hidden_WhenValueAtOrAboveRecommended()
    {
        Assert.True(RetentionThreshold.IsHidden("30", "30"));
        Assert.True(RetentionThreshold.IsHidden("90", "30"));
    }

    [Fact]
    public void Visible_WhenBelowRecommended()
    {
        Assert.False(RetentionThreshold.IsHidden("7", "30"));
    }

    [Fact]
    public void Visible_WhenNoRecommendedOrNonNumeric()
    {
        Assert.False(RetentionThreshold.IsHidden("7", null));
        Assert.False(RetentionThreshold.IsHidden("7", ""));
        Assert.False(RetentionThreshold.IsHidden("abc", "30"));
    }
}
