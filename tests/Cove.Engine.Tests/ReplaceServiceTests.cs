using Cove.Engine.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ReplaceServiceTests
{
    private static string CreateTempFile(string content)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-replace-{System.Guid.NewGuid():N}.txt");
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ReplaceInFiles_ReplacesPlainText()
    {
        var path = CreateTempFile("hello world\nhello again\n");
        try
        {
            var service = new ReplaceService(NullLogger.Instance);
            var results = service.ReplaceInFiles("hello", "hi", new[] { path });
            var result = Assert.Single(results);
            Assert.Equal(2, result.Replacements);
            Assert.True(result.Saved);
            var content = System.IO.File.ReadAllText(path);
            Assert.Equal("hi world\nhi again\n", content);
        }
        finally { try { System.IO.File.Delete(path); } catch { } }
    }

    [Fact]
    public void ReplaceInFiles_CaseInsensitive()
    {
        var path = CreateTempFile("Hello HELLO hello\n");
        try
        {
            var service = new ReplaceService(NullLogger.Instance);
            var results = service.ReplaceInFiles("hello", "hi", new[] { path }, caseInsensitive: true);
            Assert.Equal(3, results[0].Replacements);
        }
        finally { try { System.IO.File.Delete(path); } catch { } }
    }

    [Fact]
    public void ReplaceInFiles_CaseSensitive()
    {
        var path = CreateTempFile("Hello hello HELLO\n");
        try
        {
            var service = new ReplaceService(NullLogger.Instance);
            var results = service.ReplaceInFiles("hello", "hi", new[] { path }, caseInsensitive: false);
            Assert.Equal(1, results[0].Replacements);
        }
        finally { try { System.IO.File.Delete(path); } catch { } }
    }

    [Fact]
    public void ReplaceInFiles_Regex()
    {
        var path = CreateTempFile("foo123 bar456 baz789\n");
        try
        {
            var service = new ReplaceService(NullLogger.Instance);
            var results = service.ReplaceInFiles(@"\d+", "NUM", new[] { path }, useRegex: true, caseInsensitive: false);
            Assert.Equal(3, results[0].Replacements);
            var content = System.IO.File.ReadAllText(path);
            Assert.Equal("fooNUM barNUM bazNUM\n", content);
        }
        finally { try { System.IO.File.Delete(path); } catch { } }
    }

    [Fact]
    public void ReplaceInFiles_WholeWord()
    {
        var path = CreateTempFile("hello helloworld hello\n");
        try
        {
            var service = new ReplaceService(NullLogger.Instance);
            var results = service.ReplaceInFiles("hello", "hi", new[] { path }, wholeWord: true);
            Assert.Equal(2, results[0].Replacements);
        }
        finally { try { System.IO.File.Delete(path); } catch { } }
    }

    [Fact]
    public void ReplaceInFiles_NoMatch_ReturnsZero()
    {
        var path = CreateTempFile("nothing here\n");
        try
        {
            var service = new ReplaceService(NullLogger.Instance);
            var results = service.ReplaceInFiles("missing", "found", new[] { path });
            Assert.Equal(0, results[0].Replacements);
            Assert.False(results[0].Saved);
        }
        finally { try { System.IO.File.Delete(path); } catch { } }
    }

    [Fact]
    public void ReplaceInFiles_MultipleFiles()
    {
        var path1 = CreateTempFile("foo bar\n");
        var path2 = CreateTempFile("foo baz\n");
        try
        {
            var service = new ReplaceService(NullLogger.Instance);
            var results = service.ReplaceInFiles("foo", "qux", new[] { path1, path2 });
            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.Equal(1, r.Replacements));
        }
        finally { try { System.IO.File.Delete(path1); } catch { } try { System.IO.File.Delete(path2); } catch { } }
    }

    [Fact]
    public void ReplaceInFiles_NonexistentFile_ReturnsZero()
    {
        var service = new ReplaceService(NullLogger.Instance);
        var results = service.ReplaceInFiles("foo", "bar", new[] { "/nonexistent/file.txt" });
        var result = Assert.Single(results);
        Assert.Equal(0, result.Replacements);
        Assert.False(result.Saved);
    }

    [Fact]
    public void ReplaceInFiles_EmptySearch_ReturnsEmpty()
    {
        var service = new ReplaceService(NullLogger.Instance);
        var results = service.ReplaceInFiles("", "bar", new[] { "file.txt" });
        Assert.Empty(results);
    }

    [Fact]
    public void PreviewReplace_ReturnsCountWithoutSaving()
    {
        var path = CreateTempFile("hello world hello\n");
        try
        {
            var service = new ReplaceService(NullLogger.Instance);
            var result = service.PreviewReplace("hello", "hi", path);
            Assert.Equal(2, result.Replacements);
            Assert.False(result.Saved);
            var content = System.IO.File.ReadAllText(path);
            Assert.Contains("hello", content);
        }
        finally { try { System.IO.File.Delete(path); } catch { } }
    }
}
