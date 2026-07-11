using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ReviewScopeResolverTests
{
    private static ReviewStore NewStore()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-scope-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        return new ReviewStore(dir, NullLogger.Instance);
    }

    private static ReviewScopeResolver NewResolver(ReviewStore store)
        => new(store, NullLogger.Instance);

    [Fact]
    public void Resolve_BayScope_ReturnsAllComments()
    {
        var store = NewStore();
        var resolver = NewResolver(store);
        store.AddComment("abc1234", "file.cs", 10, "alice", "root", null);
        store.AddComment("abc1234", "file.cs", 20, "alice", "another", null);
        store.AddComment("abc1234", "other.cs", 5, "alice", "third", null);

        var comments = resolver.Resolve("abc1234", new ReviewScope("bay", null));

        Assert.Equal(3, comments.Count);
    }

    [Fact]
    public void Resolve_NullScope_ReturnsAllComments()
    {
        var store = NewStore();
        var resolver = NewResolver(store);
        store.AddComment("abc1234", "file.cs", 10, "alice", "root", null);

        var comments = resolver.Resolve("abc1234", null);

        Assert.Single(comments);
    }

    [Fact]
    public void Resolve_SessionScope_ReturnsComments_WhenSessionHasTelemetry()
    {
        var store = NewStore();
        var resolver = NewResolver(store);
        store.AddComment("abc1234", "file.cs", 10, "alice", "root", null);
        store.AddTelemetry("abc1234", "session-1", "claude", 3);

        var comments = resolver.Resolve("abc1234", new ReviewScope("session", "session-1"));

        Assert.Single(comments);
    }

    [Fact]
    public void Resolve_SessionScope_ReturnsEmpty_WhenSessionNotFound()
    {
        var store = NewStore();
        var resolver = NewResolver(store);
        store.AddComment("abc1234", "file.cs", 10, "alice", "root", null);
        store.AddTelemetry("abc1234", "session-1", "claude", 3);

        var comments = resolver.Resolve("abc1234", new ReviewScope("session", "nonexistent"));

        Assert.Empty(comments);
    }

    [Fact]
    public void Resolve_SessionScope_NullId_ReturnsAll()
    {
        var store = NewStore();
        var resolver = NewResolver(store);
        store.AddComment("abc1234", "file.cs", 10, "alice", "root", null);

        var comments = resolver.Resolve("abc1234", new ReviewScope("session", null));

        Assert.Single(comments);
    }

    [Fact]
    public void Resolve_DifferentCommits_Isolated()
    {
        var store = NewStore();
        var resolver = NewResolver(store);
        store.AddComment("abc1234", "file.cs", 10, "alice", "on abc", null);
        store.AddComment("def5678", "file.cs", 10, "alice", "on def", null);

        var abcComments = resolver.Resolve("abc1234", null);
        var defComments = resolver.Resolve("def5678", null);

        Assert.Single(abcComments);
        Assert.Single(defComments);
        Assert.Equal("on abc", abcComments[0].Body);
        Assert.Equal("on def", defComments[0].Body);
    }
}
