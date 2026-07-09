using Cove.Gui;
using Xunit;

public class MediaRangeTests
{
    [Fact]
    public void NoRangeHeader_ServesWholeFile_200()
    {
        var r = MediaRange.Resolve(null, 1000);
        Assert.Equal(200, r.StatusCode);
        Assert.False(r.IsPartial);
        Assert.Equal(0, r.Start);
        Assert.Equal(999, r.End);
        Assert.Equal(1000, r.Length);
        Assert.Equal(1000, r.TotalLength);
    }

    [Fact]
    public void EmptyRangeHeader_ServesWholeFile_200()
    {
        var r = MediaRange.Resolve("   ", 1000);
        Assert.Equal(200, r.StatusCode);
        Assert.Equal(1000, r.Length);
    }

    [Fact]
    public void ClosedRange_Returns206_WithInclusiveBounds()
    {
        var r = MediaRange.Resolve("bytes=0-499", 1000);
        Assert.Equal(206, r.StatusCode);
        Assert.True(r.IsPartial);
        Assert.Equal(0, r.Start);
        Assert.Equal(499, r.End);
        Assert.Equal(500, r.Length);
        Assert.Equal(1000, r.TotalLength);
    }

    [Fact]
    public void OpenEndedRange_RunsToEndOfFile()
    {
        var r = MediaRange.Resolve("bytes=500-", 1000);
        Assert.Equal(206, r.StatusCode);
        Assert.Equal(500, r.Start);
        Assert.Equal(999, r.End);
        Assert.Equal(500, r.Length);
    }

    [Fact]
    public void SuffixRange_ReturnsLastBytes()
    {
        var r = MediaRange.Resolve("bytes=-200", 1000);
        Assert.Equal(206, r.StatusCode);
        Assert.Equal(800, r.Start);
        Assert.Equal(999, r.End);
        Assert.Equal(200, r.Length);
    }

    [Fact]
    public void SuffixLargerThanFile_ClampsToWholeFile()
    {
        var r = MediaRange.Resolve("bytes=-5000", 1000);
        Assert.Equal(206, r.StatusCode);
        Assert.Equal(0, r.Start);
        Assert.Equal(999, r.End);
        Assert.Equal(1000, r.Length);
    }

    [Fact]
    public void EndBeyondFile_ClampsToLastByte()
    {
        var r = MediaRange.Resolve("bytes=900-9999", 1000);
        Assert.Equal(206, r.StatusCode);
        Assert.Equal(900, r.Start);
        Assert.Equal(999, r.End);
        Assert.Equal(100, r.Length);
    }

    [Fact]
    public void StartBeyondFile_Returns416()
    {
        var r = MediaRange.Resolve("bytes=1000-1100", 1000);
        Assert.Equal(416, r.StatusCode);
        Assert.Equal(1000, r.TotalLength);
    }

    [Fact]
    public void StartGreaterThanEnd_Returns416()
    {
        var r = MediaRange.Resolve("bytes=500-100", 1000);
        Assert.Equal(416, r.StatusCode);
    }

    [Fact]
    public void MalformedRange_Returns416()
    {
        var r = MediaRange.Resolve("bytes=abc-def", 1000);
        Assert.Equal(416, r.StatusCode);
    }

    [Fact]
    public void ZeroSuffix_Returns416()
    {
        var r = MediaRange.Resolve("bytes=-0", 1000);
        Assert.Equal(416, r.StatusCode);
    }

    [Fact]
    public void MultiRange_Unsupported_ServesWholeFile_200()
    {
        var r = MediaRange.Resolve("bytes=0-99,200-299", 1000);
        Assert.Equal(200, r.StatusCode);
        Assert.Equal(1000, r.Length);
    }

    [Fact]
    public void NonBytesUnit_ServesWholeFile_200()
    {
        var r = MediaRange.Resolve("items=0-10", 1000);
        Assert.Equal(200, r.StatusCode);
    }
}
