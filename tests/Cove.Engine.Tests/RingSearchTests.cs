using System.Text;
using Cove.Engine.Pty;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests.Pty;

public sealed class RingSearchTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Find_PlainText_MatchesLines()
    {
        var content = B("hello world\nfoo bar\nhello again\n");
        var matches = RingSearch.Find(content, "hello", false);
        Assert.Equal(2, matches.Length);
        Assert.Equal(0, matches[0].Line);
        Assert.Equal("hello world", matches[0].Text);
        Assert.Equal(2, matches[1].Line);
        Assert.Equal("hello again", matches[1].Text);
    }

    [Fact]
    public void Find_StripsAnsi()
    {
        var content = B("\u001b[31mERROR\u001b[0m here\n");
        var matches = RingSearch.Find(content, "ERROR here", true);
        Assert.Single(matches);
        Assert.Equal("ERROR here", matches[0].Text);
        Assert.DoesNotContain('\u001b', matches[0].Text);
    }

    [Fact]
    public void Find_CaseInsensitiveByDefault()
    {
        var content = B("hello\n");
        Assert.Single(RingSearch.Find(content, "HELLO", false));
        Assert.Empty(RingSearch.Find(content, "HELLO", true));
    }

    [Fact]
    public void Find_EmptyQuery_NoMatches()
    {
        var content = B("hello world\n");
        Assert.Empty(RingSearch.Find(content, "", false));
        Assert.Empty(RingSearch.Find(content, null!, false));
    }
}
