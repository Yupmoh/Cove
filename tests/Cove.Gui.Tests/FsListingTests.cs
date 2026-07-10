using System.Text.Json;
using Cove.Gui;
using Xunit;

public class FsListingTests
{
    [Fact]
    public void ListsFilesAndDirectoriesWithKinds()
    {
        var root = Directory.CreateTempSubdirectory("cove-fslist").FullName;
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        File.WriteAllText(Path.Combine(root, "readme.md"), "hi");

        var json = FsListing.ListDirectory(root, 100);
        using var doc = JsonDocument.Parse(json);
        var entries = doc.RootElement.GetProperty("entries").EnumerateArray().ToList();

        Assert.Equal(2, entries.Count);
        var sub = entries.Single(e => e.GetProperty("name").GetString() == "sub");
        var readme = entries.Single(e => e.GetProperty("name").GetString() == "readme.md");
        Assert.True(sub.GetProperty("isDir").GetBoolean());
        Assert.False(readme.GetProperty("isDir").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("truncated").GetBoolean());
        Directory.Delete(root, true);
    }

    [Fact]
    public void ReportsMissingDirectoryAsError()
    {
        var json = FsListing.ListDirectory("/nonexistent/cove-fslist-nope", 100);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("not_found", doc.RootElement.GetProperty("error").GetString());
        Assert.Empty(doc.RootElement.GetProperty("entries").EnumerateArray());
    }

    [Fact]
    public void CapsEntryCountAndFlagsTruncation()
    {
        var root = Directory.CreateTempSubdirectory("cove-fslist-cap").FullName;
        for (var i = 0; i < 10; i++) File.WriteAllText(Path.Combine(root, $"f{i}.txt"), "");

        var json = FsListing.ListDirectory(root, 4);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(4, doc.RootElement.GetProperty("entries").GetArrayLength());
        Assert.True(doc.RootElement.GetProperty("truncated").GetBoolean());
        Directory.Delete(root, true);
    }
}
