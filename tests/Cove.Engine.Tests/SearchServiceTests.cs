using Cove.Engine.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SearchServiceTests
{
    private static string CreateTestRepo()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-search-test-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "file1.txt"), "hello world\nfoo bar\nhello again\n");
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "file2.ts"), "const x = 1;\nconst hello = 'world';\n");
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "exclude.me"), "hello excluded\n");
        return dir;
    }

    [Fact]
    public async Task SearchAsync_FindsMatches()
    {
        var dir = CreateTestRepo();
        try
        {
            var service = new SearchService(NullLogger.Instance);
            if (!service.IsAvailable) return;

            var result = await service.SearchAsync(new SearchParams("hello", dir, false, false, true, null, null));
            Assert.NotEmpty(result.Matches);
            Assert.Contains(result.Matches, m => m.FilePath.EndsWith("file1.txt"));
            Assert.Contains(result.Matches, m => m.FilePath.EndsWith("file2.ts"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task SearchAsync_CaseInsensitive_FindsLowercase()
    {
        var dir = CreateTestRepo();
        try
        {
            var service = new SearchService(NullLogger.Instance);
            if (!service.IsAvailable) return;

            var result = await service.SearchAsync(new SearchParams("HELLO", dir, false, false, true, null, null));
            Assert.NotEmpty(result.Matches);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task SearchAsync_CaseSensitive_NoMatchForWrongCase()
    {
        var dir = CreateTestRepo();
        try
        {
            var service = new SearchService(NullLogger.Instance);
            if (!service.IsAvailable) return;

            var result = await service.SearchAsync(new SearchParams("HELLO", dir, false, false, false, null, null));
            Assert.Empty(result.Matches);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task SearchAsync_Regex_FindsPattern()
    {
        var dir = CreateTestRepo();
        try
        {
            var service = new SearchService(NullLogger.Instance);
            if (!service.IsAvailable) return;

            var result = await service.SearchAsync(new SearchParams("const\\s+\\w+", dir, true, false, false, "*.ts", null));
            Assert.NotEmpty(result.Matches);
            Assert.All(result.Matches, m => Assert.EndsWith(".ts", m.FilePath));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task SearchAsync_IncludeGlob_FiltersByFilePattern()
    {
        var dir = CreateTestRepo();
        try
        {
            var service = new SearchService(NullLogger.Instance);
            if (!service.IsAvailable) return;

            var result = await service.SearchAsync(new SearchParams("hello", dir, false, false, true, "*.txt", null));
            Assert.NotEmpty(result.Matches);
            Assert.All(result.Matches, m => Assert.EndsWith(".txt", m.FilePath));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task SearchAsync_ExcludeGlob_ExcludesFiles()
    {
        var dir = CreateTestRepo();
        try
        {
            var service = new SearchService(NullLogger.Instance);
            if (!service.IsAvailable) return;

            var result = await service.SearchAsync(new SearchParams("hello", dir, false, false, true, null, "*.me"));
            Assert.NotEmpty(result.Matches);
            Assert.DoesNotContain(result.Matches, m => m.FilePath.EndsWith(".me"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmpty()
    {
        var service = new SearchService(NullLogger.Instance);
        var result = await service.SearchAsync(new SearchParams("", null, false, false, true, null, null));
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task SearchAsync_ReturnsLineAndColumn()
    {
        var dir = CreateTestRepo();
        try
        {
            var service = new SearchService(NullLogger.Instance);
            if (!service.IsAvailable) return;

            var result = await service.SearchAsync(new SearchParams("foo", dir, false, false, true, null, null));
            var match = Assert.Single(result.Matches);
            Assert.True(match.Line > 0);
            Assert.True(match.Column > 0);
            Assert.Contains("foo", match.Text);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }
}
