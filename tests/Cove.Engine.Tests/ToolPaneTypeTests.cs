using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ToolPaneTypeTests
{
    [Theory]
    [InlineData("tasks-list", PaneType.Tasks)]
    [InlineData("notepad", PaneType.Notepad)]
    [InlineData("sourceControl", PaneType.SourceControl)]
    public void PaneTypeConverter_RoundTripsToolPanes(string wire, PaneType expected)
    {
        var json = $"{{\"documentId\":\"tp1\",\"paneType\":\"{wire}\",\"title\":null}}";
        var sub = System.Text.Json.JsonSerializer.Deserialize(json, CoveJsonContext.Default.Subtab)!;
        Assert.Equal(expected, sub.PaneType);
        var roundTripped = System.Text.Json.JsonSerializer.Serialize(sub, CoveJsonContext.Default.Subtab);
        Assert.Contains($"\"{wire}\"", roundTripped);
    }
}
