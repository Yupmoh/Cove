using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ReviewReadyDismissalStoreTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-dismissal-{System.Guid.NewGuid():N}");

    [Fact]
    public void Dismiss_AddsKey()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewReadyDismissalStore(dir, NullLogger.Instance);
            store.Dismiss("review-1");
            Assert.True(store.IsDismissed("review-1"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void IsDismissed_NotDismissed_ReturnsFalse()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewReadyDismissalStore(dir, NullLogger.Instance);
            Assert.False(store.IsDismissed("review-1"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Dismiss_PersistsAcrossInstances()
    {
        var dir = NewDir();
        try
        {
            var store1 = new ReviewReadyDismissalStore(dir, NullLogger.Instance);
            store1.Dismiss("review-1");
            var store2 = new ReviewReadyDismissalStore(dir, NullLogger.Instance);
            Assert.True(store2.IsDismissed("review-1"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Restore_RemovesKey()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewReadyDismissalStore(dir, NullLogger.Instance);
            store.Dismiss("review-1");
            Assert.True(store.Restore("review-1"));
            Assert.False(store.IsDismissed("review-1"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Restore_NotDismissed_ReturnsFalse()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewReadyDismissalStore(dir, NullLogger.Instance);
            Assert.False(store.Restore("nonexistent"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ListDismissed_ReturnsAll()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewReadyDismissalStore(dir, NullLogger.Instance);
            store.Dismiss("review-1");
            store.Dismiss("review-2");
            var list = store.ListDismissed();
            Assert.Equal(2, list.Count);
            Assert.Contains("review-1", list);
            Assert.Contains("review-2", list);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ClearAll_RemovesAll()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewReadyDismissalStore(dir, NullLogger.Instance);
            store.Dismiss("review-1");
            store.Dismiss("review-2");
            store.ClearAll();
            Assert.Empty(store.ListDismissed());
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Dismiss_DuplicateKey_DoesNotDuplicate()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewReadyDismissalStore(dir, NullLogger.Instance);
            store.Dismiss("review-1");
            store.Dismiss("review-1");
            Assert.Single(store.ListDismissed());
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Dismiss_EmptyKey_DoesNothing()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewReadyDismissalStore(dir, NullLogger.Instance);
            store.Dismiss("");
            Assert.Empty(store.ListDismissed());
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void IsDismissed_EmptyKey_ReturnsFalse()
    {
        var dir = NewDir();
        try
        {
            var store = new ReviewReadyDismissalStore(dir, NullLogger.Instance);
            Assert.False(store.IsDismissed(""));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }
}
