using System;
using System.Text;
using Cove.Engine.Pty;
using Xunit;

namespace Cove.Engine.Tests.Pty;

public sealed class Osc7ParserTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Parses_Bel_Terminated()
    {
        var p = new Osc7Parser();
        var r = p.Feed(B("\u001b]7;file://host/Users/moh/proj\u0007"));
        Assert.Equal("/Users/moh/proj", r);
    }

    [Fact]
    public void Parses_St_Terminated()
    {
        var p = new Osc7Parser();
        var r = p.Feed(B("\u001b]7;file://h/tmp\u001b\\"));
        Assert.Equal("/tmp", r);
    }

    [Fact]
    public void Parses_Across_Chunks()
    {
        var p = new Osc7Parser();
        Assert.Null(p.Feed(B("\u001b]7;file://h/a")));
        var r = p.Feed(B("bc/d\u0007"));
        Assert.Equal("/abc/d", r);
    }

    [Fact]
    public void Ignores_NonOsc7()
    {
        var p = new Osc7Parser();
        Assert.Null(p.Feed(B("hello world\n")));
    }

    [Fact]
    public void Handles_Noise_Around()
    {
        var p = new Osc7Parser();
        var r = p.Feed(B("x\u001b]7;file://h/tmp\u0007y"));
        Assert.Equal("/tmp", r);
    }

    [Fact]
    public void Percent_Decodes()
    {
        var p = new Osc7Parser();
        var r = p.Feed(B("\u001b]7;file://h/a%20b\u0007"));
        Assert.Equal("/a b", r);
    }

    [Fact]
    public void Overlong_DoesNotThrow()
    {
        var p = new Osc7Parser();
        var sb = new StringBuilder();
        sb.Append("\u001b]7;file://h/");
        sb.Append('a', 4100);
        Assert.Null(p.Feed(B(sb.ToString())));
        var r = p.Feed(B("\u001b]7;file://h/tmp\u0007"));
        Assert.Equal("/tmp", r);
    }
}
