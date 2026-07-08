using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ReviewStoreTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-review-{System.Guid.NewGuid():N}");

    private static ReviewStore NewStore()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        return new ReviewStore(dir, NullLogger.Instance);
    }

    [Fact]
    public void AddComment_PersistsThreadedComment()
    {
        var store = NewStore();
        var comment = store.AddComment("abc1234", "file.cs", 10, "alice", "This needs a null check", null);

        Assert.False(string.IsNullOrEmpty(comment.Id));
        Assert.Equal("abc1234", comment.CommitSha);
        Assert.Equal("file.cs", comment.FilePath);
        Assert.Equal(10, comment.Line);
        Assert.Equal("alice", comment.Author);
        Assert.Equal("This needs a null check", comment.Body);
        Assert.Equal("open", comment.State);
        Assert.Null(comment.ParentId);
    }

    [Fact]
    public void AddComment_RootComment_HasSelfRootId()
    {
        var store = NewStore();
        var comment = store.AddComment("abc1234", "file.cs", 10, "alice", "root comment", null);

        Assert.Equal(comment.Id, comment.RootId);
    }

    [Fact]
    public void AddComment_Reply_InheritsRootId()
    {
        var store = NewStore();
        var root = store.AddComment("abc1234", "file.cs", 10, "alice", "root", null);
        var reply = store.AddComment("abc1234", "file.cs", 10, "bob", "reply", root.Id);

        Assert.Equal(root.Id, reply.RootId);
        Assert.Equal(root.Id, reply.ParentId);
    }

    [Fact]
    public void ResolveComment_TransitionsToResolved_WithAuditRow()
    {
        var store = NewStore();
        var comment = store.AddComment("abc1234", "file.cs", 10, "alice", "needs fix", null);

        store.ResolveComment(comment.Id, "alice");

        var retrieved = store.GetComment(comment.Id)!;
        Assert.Equal("resolved", retrieved.State);

        var audit = store.GetAuditTrail(comment.Id);
        Assert.Single(audit);
        Assert.Equal("resolved", audit[0].ToState);
        Assert.Equal("alice", audit[0].Actor);
    }

    [Fact]
    public void ReopenComment_TransitionsToOpen_WithAuditRow()
    {
        var store = NewStore();
        var comment = store.AddComment("abc1234", "file.cs", 10, "alice", "needs fix", null);
        store.ResolveComment(comment.Id, "alice");
        store.ReopenComment(comment.Id, "bob");

        var retrieved = store.GetComment(comment.Id)!;
        Assert.Equal("open", retrieved.State);

        var audit = store.GetAuditTrail(comment.Id);
        Assert.Equal(2, audit.Count);
        Assert.Equal("resolved", audit[0].ToState);
        Assert.Equal("open", audit[1].ToState);
    }

    [Fact]
    public void CloseComment_TransitionsToClosed()
    {
        var store = NewStore();
        var comment = store.AddComment("abc1234", "file.cs", 10, "alice", "needs fix", null);
        store.CloseComment(comment.Id, "alice");

        var retrieved = store.GetComment(comment.Id)!;
        Assert.Equal("closed", retrieved.State);
    }

    [Fact]
    public void ListComments_ReturnsThreadForCommit()
    {
        var store = NewStore();
        var root = store.AddComment("abc1234", "file.cs", 10, "alice", "root", null);
        store.AddComment("abc1234", "file.cs", 10, "bob", "reply", root.Id);
        store.AddComment("abc1234", "file.cs", 20, "alice", "another", null);
        store.AddComment("def5678", "file.cs", 10, "alice", "other commit", null);

        var comments = store.ListComments("abc1234");
        Assert.Equal(3, comments.Count);
        Assert.DoesNotContain(comments, c => c.CommitSha == "def5678");
    }

    [Fact]
    public void ListComments_FiltersByFilePath()
    {
        var store = NewStore();
        store.AddComment("abc1234", "file.cs", 10, "alice", "on file.cs", null);
        store.AddComment("abc1234", "other.cs", 20, "alice", "on other.cs", null);

        var comments = store.ListComments("abc1234", filePath: "file.cs");
        Assert.Single(comments);
        Assert.Equal("file.cs", comments[0].FilePath);
    }

    [Fact]
    public void ListComments_FiltersByState()
    {
        var store = NewStore();
        var open = store.AddComment("abc1234", "file.cs", 10, "alice", "open", null);
        var toResolve = store.AddComment("abc1234", "file.cs", 20, "alice", "will resolve", null);
        store.ResolveComment(toResolve.Id, "alice");

        var openComments = store.ListComments("abc1234", state: "open");
        Assert.Single(openComments);
        Assert.Equal(open.Id, openComments[0].Id);

        var resolvedComments = store.ListComments("abc1234", state: "resolved");
        Assert.Single(resolvedComments);
        Assert.Equal(toResolve.Id, resolvedComments[0].Id);
    }

    [Fact]
    public void OrphanComment_SetsOrphanedAt()
    {
        var store = NewStore();
        var comment = store.AddComment("abc1234", "file.cs", 10, "alice", "needs fix", null);
        store.OrphanComment(comment.Id);

        var retrieved = store.GetComment(comment.Id)!;
        Assert.Equal("orphaned", retrieved.State);
        Assert.NotNull(retrieved.OrphanedAt);
    }

    [Fact]
    public void ReAnchorComment_MovesLine_AndReopens()
    {
        var store = NewStore();
        var comment = store.AddComment("abc1234", "file.cs", 10, "alice", "needs fix", null);
        store.OrphanComment(comment.Id);

        store.ReAnchorComment(comment.Id, 25);

        var retrieved = store.GetComment(comment.Id)!;
        Assert.Equal(25, retrieved.Line);
        Assert.Equal("open", retrieved.State);
        Assert.Null(retrieved.OrphanedAt);

        var audit = store.GetAuditTrail(comment.Id);
        Assert.Equal(2, audit.Count);
        Assert.Equal("orphaned", audit[0].ToState);
        Assert.Equal("open", audit[1].ToState);
    }

    [Fact]
    public void AddTelemetry_AccruesPerCommit()
    {
        var store = NewStore();
        store.AddTelemetry("abc1234", "session-1", "claude", 3);
        store.AddTelemetry("abc1234", "session-2", "opus", 5);
        store.AddTelemetry("abc1234", "session-1", "claude", 2);

        var telemetry = store.GetTelemetry("abc1234");
        Assert.Equal(2, telemetry.Count);
        var s1 = telemetry.First(t => t.SessionId == "session-1");
        Assert.Equal("claude", s1.Adapter);
        Assert.Equal(5, s1.FilesTouched);
    }

    [Fact]
    public void GetComment_Nonexistent_ReturnsNull()
    {
        var store = NewStore();
        Assert.Null(store.GetComment("nonexistent"));
    }

    [Fact]
    public void GetAuditTrail_Nonexistent_ReturnsEmpty()
    {
        var store = NewStore();
        Assert.Empty(store.GetAuditTrail("nonexistent"));
    }
}
