using Cove.Adapters;
using Cove.Protocol;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class EnvVarParserTests
{
    [Fact]
    public void Parse_SimpleAssignment()
    {
        var entries = EnvVarParser.Parse("FOO=bar");
        Assert.Single(entries);
        Assert.Equal("FOO", entries[0].Key);
        Assert.Equal("bar", entries[0].Value);
        Assert.True(entries[0].Enabled);
    }

    [Fact]
    public void Parse_ExportPrefix()
    {
        var entries = EnvVarParser.Parse("export FOO=bar");
        Assert.Single(entries);
        Assert.Equal("FOO", entries[0].Key);
        Assert.Equal("bar", entries[0].Value);
    }

    [Fact]
    public void Parse_QuotedValue_StripsQuotes()
    {
        var entries = EnvVarParser.Parse("FOO=\"hello world\"");
        Assert.Single(entries);
        Assert.Equal("hello world", entries[0].Value);

        var single = EnvVarParser.Parse("BAR='baz'");
        Assert.Equal("baz", single[0].Value);
    }

    [Fact]
    public void Parse_CommentLine_Skipped()
    {
        var entries = EnvVarParser.Parse("# this is a comment\nFOO=bar");
        Assert.Single(entries);
        Assert.Equal("FOO", entries[0].Key);
    }

    [Fact]
    public void Parse_InlineComment_NotStripped()
    {
        var entries = EnvVarParser.Parse("FOO=bar # not a comment");
        Assert.Single(entries);
        Assert.Equal("bar # not a comment", entries[0].Value);
    }

    [Fact]
    public void Parse_BlankLines_Skipped()
    {
        var entries = EnvVarParser.Parse("\n\nFOO=bar\n\n");
        Assert.Single(entries);
    }

    [Fact]
    public void Parse_PastedEnvBlock_Expands()
    {
        var block = """
        # my env
        API_KEY=secret123
        export DEBUG=true
        PORT=8080
        """;
        var entries = EnvVarParser.Parse(block);
        Assert.Equal(3, entries.Count);
        Assert.Equal("API_KEY", entries[0].Key);
        Assert.Equal("DEBUG", entries[1].Key);
        Assert.Equal("PORT", entries[2].Key);
    }

    [Fact]
    public void Parse_NoValue_EmptyString()
    {
        var entries = EnvVarParser.Parse("EMPTY=");
        Assert.Single(entries);
        Assert.Equal("", entries[0].Value);
    }

    [Fact]
    public void Parse_InvalidLine_NoAssignment_Skipped()
    {
        var entries = EnvVarParser.Parse("NOT_AN_ASSIGNMENT\nFOO=bar");
        Assert.Single(entries);
        Assert.Equal("FOO", entries[0].Key);
    }

    [Fact]
    public void MaskSecret_ReplacesValue()
    {
        Assert.Equal("****", EnvVarParser.MaskSecret("API_KEY", "secret123"));
        Assert.Equal("****", EnvVarParser.MaskSecret("AUTH_TOKEN", "tok_abc"));
        Assert.Equal("****", EnvVarParser.MaskSecret("DB_PASSWORD", "hunter2"));
        Assert.Equal("****", EnvVarParser.MaskSecret("MY_SECRET", "xyz"));
    }

    [Fact]
    public void MaskSecret_NonSecret_Unchanged()
    {
        Assert.Equal("bar", EnvVarParser.MaskSecret("FOO", "bar"));
        Assert.Equal("8080", EnvVarParser.MaskSecret("PORT", "8080"));
        Assert.Equal("true", EnvVarParser.MaskSecret("DEBUG", "true"));
    }
}
