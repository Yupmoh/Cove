using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ReviewedFileStoreTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-reviewed-{System.Guid.NewGuid():N}");

    [Fact]
    public void MarkReviewed_PersistsToFile()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewedFileStore(dir, NullLogger.Instance);
            store.MarkReviewed("/repo", "main", "src/file.cs", "user");
            Assert.True(store.IsReviewed("/repo", "main", "src/file.cs"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void IsReviewed_ReturnsFalse_WhenNotMarked()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewedFileStore(dir, NullLogger.Instance);
            Assert.False(store.IsReviewed("/repo", "main", "src/file.cs"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void IsReviewed_ScopedPerRepoAndScope()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewedFileStore(dir, NullLogger.Instance);
            store.MarkReviewed("/repo-a", "main", "file.cs", "user");
            Assert.True(store.IsReviewed("/repo-a", "main", "file.cs"));
            Assert.False(store.IsReviewed("/repo-b", "main", "file.cs"));
            Assert.False(store.IsReviewed("/repo-a", "feature", "file.cs"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ListReviewed_ReturnsAllForScope()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewedFileStore(dir, NullLogger.Instance);
            store.MarkReviewed("/repo", "main", "a.cs", "user1");
            store.MarkReviewed("/repo", "main", "b.cs", "user2");
            var list = store.ListReviewed("/repo", "main");
            Assert.Equal(2, list.Count);
            Assert.Contains(list, f => f.FilePath == "a.cs" && f.ReviewedBy == "user1");
            Assert.Contains(list, f => f.FilePath == "b.cs" && f.ReviewedBy == "user2");
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void UnmarkReviewed_RemovesEntry()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewedFileStore(dir, NullLogger.Instance);
            store.MarkReviewed("/repo", "main", "file.cs", "user");
            Assert.True(store.IsReviewed("/repo", "main", "file.cs"));

            var removed = store.UnmarkReviewed("/repo", "main", "file.cs");
            Assert.True(removed);
            Assert.False(store.IsReviewed("/repo", "main", "file.cs"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void UnmarkReviewed_ReturnsFalse_WhenNotMarked()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewedFileStore(dir, NullLogger.Instance);
            Assert.False(store.UnmarkReviewed("/repo", "main", "file.cs"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void MarkReviewed_OverwritesPreviousMark()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewedFileStore(dir, NullLogger.Instance);
            store.MarkReviewed("/repo", "main", "file.cs", "user1");
            store.MarkReviewed("/repo", "main", "file.cs", "user2");
            var list = store.ListReviewed("/repo", "main");
            Assert.Single(list);
            Assert.Equal("user2", list[0].ReviewedBy);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void MarkReviewed_ThrowsOnEmptyRepoRoot()
    {
        var store = new ReviewedFileStore(NewDir(), NullLogger.Instance);
        Assert.Throws<ArgumentException>(() => store.MarkReviewed("", "main", "file.cs", "user"));
    }

    [Fact]
    public void MarkReviewed_ThrowsOnEmptyScope()
    {
        var store = new ReviewedFileStore(NewDir(), NullLogger.Instance);
        Assert.Throws<ArgumentException>(() => store.MarkReviewed("/repo", "", "file.cs", "user"));
    }
}
