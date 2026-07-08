using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class EditsIndexTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-edits-" + System.Guid.NewGuid().ToString("N"));

    private static (string dir, EditsIndex index, SessionCorpusIndexer corpus) NewIndex()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();
        var corpus = new SessionCorpusIndexer(dir, NullLogger.Instance);
        return (dir, new EditsIndex(dir, NullLogger.Instance), corpus);
    }

    private static string CreateSession(SessionCorpusIndexer corpus, string workspaceId = "ws1")
        => corpus.IndexSession(workspaceId, "claude", System.DateTimeOffset.UtcNow.ToString("o"), "session content", "v1.0");

    [Fact]
    public void RecordEdit_ThenFindByAbsolute_FindsByFilePath()
    {
        var (_, index, corpus) = NewIndex();
        var session = CreateSession(corpus);
        index.RecordEdit(session, "/Users/moh/project/src/Program.cs", "Edit", "write", "Updated main method");

        var results = index.FindByFile("/Users/moh/project/src/Program.cs");
        Assert.Single(results);
        Assert.Equal(session, results[0].SessionId);
    }

    [Fact]
    public void FindByBasename_ReturnsResultsForRelativePath()
    {
        var (_, index, corpus) = NewIndex();
        var s1 = CreateSession(corpus);
        var s2 = CreateSession(corpus);
        index.RecordEdit(s1, "/Users/moh/project/src/Program.cs", "Edit", "write", "edit 1");
        index.RecordEdit(s2, "/Users/other/project/src/Program.cs", "Edit", "write", "edit 2");

        var results = index.FindByFile("Program.cs");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void FindByFile_ResultsOrderedByRecency()
    {
        var (_, index, corpus) = NewIndex();
        var s1 = CreateSession(corpus);
        var s2 = CreateSession(corpus);
        index.RecordEdit(s1, "/path/File.cs", "Edit", "write", "old");
        System.Threading.Thread.Sleep(10);
        index.RecordEdit(s2, "/path/File.cs", "Edit", "write", "new");

        var results = index.FindByFile("/path/File.cs");
        Assert.Equal(2, results.Count);
        Assert.True(results[0].OccurredAt >= results[1].OccurredAt);
    }

    [Fact]
    public void FindBySession_ReturnsAllEditsForSession()
    {
        var (_, index, corpus) = NewIndex();
        var s1 = CreateSession(corpus);
        var s2 = CreateSession(corpus);
        index.RecordEdit(s1, "/path/A.cs", "Edit", "write", "a");
        index.RecordEdit(s1, "/path/B.cs", "Edit", "write", "b");
        index.RecordEdit(s2, "/path/C.cs", "Edit", "write", "c");

        var results = index.FindBySession(s1);
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(s1, r.SessionId));
    }

    [Fact]
    public void FindByFile_Nonexistent_ReturnsEmpty()
    {
        var (_, index, _) = NewIndex();
        var results = index.FindByFile("/nonexistent/file.cs");
        Assert.Empty(results);
    }
}
