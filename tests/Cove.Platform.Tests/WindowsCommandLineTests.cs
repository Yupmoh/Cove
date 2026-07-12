using System.Collections.Generic;
using System.Text;
using Cove.Platform.Pty.Windows;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class WindowsCommandLineTests
{
    [Fact]
    public void Quote_SimpleToken_Unchanged()
        => Assert.Equal("hello", WindowsCommandLine.Quote("hello"));

    [Fact]
    public void Quote_TokenWithSpace_IsWrapped()
        => Assert.Equal("\"hello world\"", WindowsCommandLine.Quote("hello world"));

    [Fact]
    public void Quote_EmptyArgument_BecomesEmptyQuotes()
        => Assert.Equal("\"\"", WindowsCommandLine.Quote(string.Empty));

    [Fact]
    public void Quote_EmbeddedQuotes_AreEscaped()
        => Assert.Equal("\"say \\\"hi\\\"\"", WindowsCommandLine.Quote("say \"hi\""));

    [Fact]
    public void Quote_TrailingBackslashWithoutSpecials_Unchanged()
        => Assert.Equal("a\\", WindowsCommandLine.Quote("a\\"));

    [Fact]
    public void Quote_TrailingBackslashWithSpace_IsDoubled()
        => Assert.Equal("\"a b\\\\\"", WindowsCommandLine.Quote("a b\\"));

    [Fact]
    public void Build_JoinsArgumentsWithSpaces()
        => Assert.Equal("cmd.exe /c \"echo hi\"", WindowsCommandLine.Build(new[] { "cmd.exe", "/c", "echo hi" }));

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void Build_RoundTripsThroughReferenceParser(string[] argv)
    {
        string commandLine = WindowsCommandLine.Build(argv);
        List<string> parsed = ParseLikeCommandLineToArgvW(commandLine);
        Assert.Equal(argv, parsed);
    }

    public static IEnumerable<object[]> RoundTripCases()
    {
        yield return new object[] { new[] { "cmd.exe" } };
        yield return new object[] { new[] { "cmd.exe", "/c", "echo", "hello" } };
        yield return new object[] { new[] { "a", "b c", "d" } };
        yield return new object[] { new[] { "with\"quote" } };
        yield return new object[] { new[] { "quote\"in the middle" } };
        yield return new object[] { new[] { "trailing\\", "next" } };
        yield return new object[] { new[] { "C:\\program files\\app.exe", "--flag" } };
        yield return new object[] { new[] { "back\\\\slashes" } };
        yield return new object[] { new[] { "back \\\\slashes with space" } };
        yield return new object[] { new[] { "escaped\\\"quote before space" } };
        yield return new object[] { new[] { string.Empty, "after empty" } };
        yield return new object[] { new[] { "tab\ttab", "line\nline" } };
    }

    private static List<string> ParseLikeCommandLineToArgvW(string commandLine)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        bool hasToken = false;
        int i = 0;
        while (i < commandLine.Length)
        {
            char c = commandLine[i];
            if (!inQuotes && (c == ' ' || c == '\t' || c == '\n' || c == '\v'))
            {
                if (hasToken)
                {
                    args.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }
                i++;
                continue;
            }
            if (c == '\\')
            {
                int backslashes = 0;
                while (i < commandLine.Length && commandLine[i] == '\\')
                {
                    backslashes++;
                    i++;
                }
                if (i < commandLine.Length && commandLine[i] == '"')
                {
                    current.Append('\\', backslashes / 2);
                    hasToken = true;
                    if (backslashes % 2 == 0)
                        inQuotes = !inQuotes;
                    else
                        current.Append('"');
                    i++;
                }
                else
                {
                    current.Append('\\', backslashes);
                    hasToken = true;
                }
                continue;
            }
            if (c == '"')
            {
                inQuotes = !inQuotes;
                hasToken = true;
                i++;
                continue;
            }
            current.Append(c);
            hasToken = true;
            i++;
        }
        if (hasToken)
            args.Add(current.ToString());
        return args;
    }
}
