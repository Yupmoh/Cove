using System.Text.Json;
using Cove.Gui;
using Xunit;

public class GitSummaryTests
{
    [Fact]
    public void ParsesBranchAheadBehindAndDirtyCount()
    {
        var output = "# branch.oid 1234abcd\n# branch.head main\n# branch.upstream origin/main\n# branch.ab +2 -1\n1 .M N... 100644 100644 100644 abc def src/a.ts\n? untracked.txt\n";
        var json = GitSummary.Parse(output);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("main", doc.RootElement.GetProperty("branch").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("ahead").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("behind").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("dirty").GetInt32());
    }

    [Fact]
    public void ParsesBranchWithoutUpstreamAsZeroCounts()
    {
        var output = "# branch.oid 1234abcd\n# branch.head feature/x\n";
        var json = GitSummary.Parse(output);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("feature/x", doc.RootElement.GetProperty("branch").GetString());
        Assert.Equal(0, doc.RootElement.GetProperty("ahead").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("behind").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("dirty").GetInt32());
    }

    [Fact]
    public void ParsesModifiedAddedDeletedAndUntrackedFiles()
    {
        var output = "# branch.head main\n1 .M N... 100644 100644 100644 abc def src/modified.cs\n1 A. N... 000000 100644 100644 abc def src/added.cs\n1 D. N... 100644 000000 000000 abc def src/deleted.cs\n? src/untracked.cs\n";
        var json = GitSummary.Parse(output);
        using var doc = JsonDocument.Parse(json);
        var files = doc.RootElement.GetProperty("files").EnumerateArray().ToDictionary(
            entry => entry.GetProperty("path").GetString()!,
            entry => entry.GetProperty("status").GetString());

        Assert.Equal("M", files["src/modified.cs"]);
        Assert.Equal("A", files["src/added.cs"]);
        Assert.Equal("D", files["src/deleted.cs"]);
        Assert.Equal("A", files["src/untracked.cs"]);
    }

    [Fact]
    public void RunReportsMissingDirectory()
    {
        var json = GitSummary.Run("/nonexistent/cove-git-nope");
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("not_found", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void RunReportsNonRepoDirectory()
    {
        var root = Directory.CreateTempSubdirectory("cove-git-norepo").FullName;
        var json = GitSummary.Run(root);
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("not_a_repo", doc.RootElement.GetProperty("error").GetString());
        Directory.Delete(root, true);
    }
}
