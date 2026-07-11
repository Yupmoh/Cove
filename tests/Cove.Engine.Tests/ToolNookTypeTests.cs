using Cove.Persistence;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ToolNookTypeTests
{
    [Theory]
    [InlineData("tasks-list", NookType.Tasks)]
    [InlineData("notepad", NookType.Notepad)]
    [InlineData("sourceControl", NookType.SourceControl)]
    public void NookTypeConverter_RoundTripsToolNooks(string wire, NookType expected)
    {
        var json = $"{{\"documentId\":\"tp1\",\"nookType\":\"{wire}\",\"title\":null}}";
        var sub = System.Text.Json.JsonSerializer.Deserialize(json, CoveJsonContext.Default.Subtab)!;
        Assert.Equal(expected, sub.NookType);
        var roundTripped = System.Text.Json.JsonSerializer.Serialize(sub, CoveJsonContext.Default.Subtab);
        Assert.Contains($"\"{wire}\"", roundTripped);
    }
}
