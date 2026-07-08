using Cove.Engine.Pty;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class TerminalLinkDetectorTests
{
    [Fact]
    public void Detect_Url()
    {
        var detector = new TerminalLinkDetector();
        var links = detector.Detect("see https://example.com for info");
        var link = Assert.Single(links);
        Assert.Equal(LinkKind.Url, link.Kind);
        Assert.Equal("https://example.com", link.Text);
    }

    [Fact]
    public void Detect_HttpUrl()
    {
        var detector = new TerminalLinkDetector();
        var links = detector.Detect("go to http://foo.bar/baz");
        var link = Assert.Single(links);
        Assert.Equal(LinkKind.Url, link.Kind);
        Assert.Equal("http://foo.bar/baz", link.Text);
    }

    [Fact]
    public void Detect_UnixPath()
    {
        var detector = new TerminalLinkDetector();
        var links = detector.Detect("error in /src/program.cs");
        var link = Assert.Single(links);
        Assert.Equal(LinkKind.FilePath, link.Kind);
        Assert.Equal("/src/program.cs", link.FilePath);
    }

    [Fact]
    public void Detect_UnixPathWithLine()
    {
        var detector = new TerminalLinkDetector();
        var links = detector.Detect("error in /src/program.cs:42");
        var link = Assert.Single(links);
        Assert.Equal(LinkKind.FilePath, link.Kind);
        Assert.Equal("/src/program.cs", link.FilePath);
        Assert.Equal(42, link.Line);
    }

    [Fact]
    public void Detect_UnixPathWithLineAndColumn()
    {
        var detector = new TerminalLinkDetector();
        var links = detector.Detect("error in /src/program.cs:42:10");
        var link = Assert.Single(links);
        Assert.Equal(42, link.Line);
        Assert.Equal(10, link.Column);
    }

    [Fact]
    public void Detect_WindowsPath()
    {
        var detector = new TerminalLinkDetector();
        var links = detector.Detect("error in C:\\src\\program.cs");
        var link = Assert.Single(links);
        Assert.Equal(LinkKind.FilePath, link.Kind);
        Assert.Equal("C:\\src\\program.cs", link.FilePath);
    }

    [Fact]
    public void Detect_HomePath()
    {
        var detector = new TerminalLinkDetector();
        var links = detector.Detect("config at ~/.config/cove");
        var link = Assert.Single(links);
        Assert.Equal(LinkKind.FilePath, link.Kind);
        Assert.Equal("~/.config/cove", link.FilePath);
    }

    [Fact]
    public void Detect_RelativePath()
    {
        var detector = new TerminalLinkDetector();
        var links = detector.Detect("edit file.txt");
        var link = Assert.Single(links);
        Assert.Equal(LinkKind.FilePath, link.Kind);
        Assert.Equal("file.txt", link.FilePath);
    }

    [Fact]
    public void Detect_TaskRef()
    {
        var detector = new TerminalLinkDetector();
        var links = detector.Detect("working on COVE-123");
        var link = Assert.Single(links);
        Assert.Equal(LinkKind.TaskRef, link.Kind);
        Assert.Equal("COVE-123", link.Text);
    }

    [Fact]
    public void Detect_MultipleLinks()
    {
        var detector = new TerminalLinkDetector();
        var links = detector.Detect("see https://example.com and /src/file.cs:10 and TASK-42");
        Assert.Equal(3, links.Count);
        Assert.Equal(LinkKind.Url, links[0].Kind);
        Assert.Equal(LinkKind.FilePath, links[1].Kind);
        Assert.Equal(LinkKind.TaskRef, links[2].Kind);
    }

    [Fact]
    public void Detect_EmptyText_ReturnsEmpty()
    {
        var detector = new TerminalLinkDetector();
        Assert.Empty(detector.Detect(""));
        Assert.Empty(detector.Detect("no links here"));
    }

    [Fact]
    public void Detect_UrlWithTrailingPunctuation_StripsIt()
    {
        var detector = new TerminalLinkDetector();
        var links = detector.Detect("visit https://example.com.");
        var link = Assert.Single(links);
        Assert.Equal("https://example.com", link.Text);
    }

    [Fact]
    public void Detect_LinksSortedByPosition()
    {
        var detector = new TerminalLinkDetector();
        var links = detector.Detect("/a.cs and /b.cs");
        Assert.True(links[0].StartIndex < links[1].StartIndex);
    }

    [Fact]
    public void Detect_OverlappingUrlAndPath_UrlWins()
    {
        var detector = new TerminalLinkDetector();
        var links = detector.Detect("https://example.com/path/file.cs");
        var link = Assert.Single(links);
        Assert.Equal(LinkKind.Url, link.Kind);
    }
}
