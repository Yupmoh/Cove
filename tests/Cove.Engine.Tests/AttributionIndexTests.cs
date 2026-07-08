using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AttributionIndexTests
{
    private static AttributionIndex NewIndex()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-attr-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        return new AttributionIndex(dir, NullLogger.Instance);
    }

    [Fact]
    public void Record_PersistsAttribution()
    {
        var idx = NewIndex();
        var entry = idx.Record("session-1", "tool-use-abc", "file.cs", 10, 20);

        Assert.False(string.IsNullOrEmpty(entry.Id));
        Assert.Equal("session-1", entry.SessionId);
        Assert.Equal("tool-use-abc", entry.ToolUseId);
        Assert.Equal("file.cs", entry.FilePath);
        Assert.Equal(10, entry.StartLine);
        Assert.Equal(20, entry.EndLine);
    }

    [Fact]
    public void FindByLine_ExactMatch_ReturnsEntry()
    {
        var idx = NewIndex();
        idx.Record("session-1", "tool-use-abc", "file.cs", 10, 20);

        var entry = idx.FindByLine("file.cs", 15);

        Assert.NotNull(entry);
        Assert.Equal("tool-use-abc", entry!.ToolUseId);
    }

    [Fact]
    public void FindByLine_BoundaryStart_ReturnsEntry()
    {
        var idx = NewIndex();
        idx.Record("session-1", "tool-use-abc", "file.cs", 10, 20);

        var entry = idx.FindByLine("file.cs", 10);

        Assert.NotNull(entry);
        Assert.Equal("tool-use-abc", entry!.ToolUseId);
    }

    [Fact]
    public void FindByLine_BoundaryEnd_ReturnsEntry()
    {
        var idx = NewIndex();
        idx.Record("session-1", "tool-use-abc", "file.cs", 10, 20);

        var entry = idx.FindByLine("file.cs", 20);

        Assert.NotNull(entry);
        Assert.Equal("tool-use-abc", entry!.ToolUseId);
    }

    [Fact]
    public void FindByLine_OutsideRange_ReturnsNull()
    {
        var idx = NewIndex();
        idx.Record("session-1", "tool-use-abc", "file.cs", 10, 20);

        var entry = idx.FindByLine("file.cs", 21);

        Assert.Null(entry);
    }

    [Fact]
    public void FindByLine_MultipleEntries_ReturnsMostRecent()
    {
        var idx = NewIndex();
        idx.Record("session-1", "tool-old", "file.cs", 10, 20);
        System.Threading.Thread.Sleep(10);
        idx.Record("session-2", "tool-new", "file.cs", 10, 20);

        var entry = idx.FindByLine("file.cs", 15);

        Assert.NotNull(entry);
        Assert.Equal("tool-new", entry!.ToolUseId);
    }

    [Fact]
    public void FindByRange_ReturnsOverlappingEntries()
    {
        var idx = NewIndex();
        idx.Record("session-1", "tool-a", "file.cs", 5, 10);
        idx.Record("session-2", "tool-b", "file.cs", 15, 20);
        idx.Record("session-3", "tool-c", "file.cs", 25, 30);

        var entries = idx.FindByRange("file.cs", 8, 17);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.ToolUseId == "tool-a");
        Assert.Contains(entries, e => e.ToolUseId == "tool-b");
    }

    [Fact]
    public void FindByToolUse_ReturnsAllForToolCall()
    {
        var idx = NewIndex();
        idx.Record("session-1", "tool-use-xyz", "file-a.cs", 1, 5);
        idx.Record("session-1", "tool-use-xyz", "file-b.cs", 10, 15);
        idx.Record("session-2", "tool-use-other", "file-a.cs", 1, 5);

        var entries = idx.FindByToolUse("tool-use-xyz");

        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal("tool-use-xyz", e.ToolUseId));
    }

    [Fact]
    public void FindByLine_DifferentFile_ReturnsNull()
    {
        var idx = NewIndex();
        idx.Record("session-1", "tool-use-abc", "file.cs", 10, 20);

        var entry = idx.FindByLine("other.cs", 15);

        Assert.Null(entry);
    }

    [Fact]
    public void FindByLine_Nonexistent_ReturnsNull()
    {
        var idx = NewIndex();

        Assert.Null(idx.FindByLine("file.cs", 1));
    }
}
