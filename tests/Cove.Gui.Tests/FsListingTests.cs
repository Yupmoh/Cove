using System.Text.Json;
using Cove.Gui;
using Cove.Gui.Tests;
using Xunit;

public class FsListingTests
{
    [Fact]
    public void ListsFilesAndDirectoriesWithKinds()
    {
        using var temp = GuiTestDirectory.Create("cove-fslist-");
        Directory.CreateDirectory(Path.Combine(temp.Path, "sub"));
        File.WriteAllText(Path.Combine(temp.Path, "readme.md"), "hi");

        var json = FsListing.ListDirectory(temp.Path, 100);
        using var doc = JsonDocument.Parse(json);
        var entries = doc.RootElement.GetProperty("entries").EnumerateArray().ToList();

        Assert.Equal(2, entries.Count);
        var sub = entries.Single(e => e.GetProperty("name").GetString() == "sub");
        var readme = entries.Single(e => e.GetProperty("name").GetString() == "readme.md");
        Assert.True(sub.GetProperty("isDir").GetBoolean());
        Assert.False(readme.GetProperty("isDir").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("truncated").GetBoolean());
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
        using var temp = GuiTestDirectory.Create("cove-fslist-cap-");
        for (var i = 0; i < 10; i++) File.WriteAllText(Path.Combine(temp.Path, $"f{i}.txt"), "");

        var json = FsListing.ListDirectory(temp.Path, 4);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(4, doc.RootElement.GetProperty("entries").GetArrayLength());
        Assert.True(doc.RootElement.GetProperty("truncated").GetBoolean());
    }
}
