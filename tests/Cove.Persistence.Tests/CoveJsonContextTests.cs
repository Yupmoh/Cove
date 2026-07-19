using System.Text.Json;
using Cove.Persistence;
using Cove.Platform;
using Xunit;

namespace Cove.Persistence.Tests;

public sealed class CoveJsonContextTests
{
    [Fact]
    public void CoveState_SerializesCamelCaseIndented()
    {
        var state = new CoveState
        {
            FocusedBay = "ws-1",
            OpenBays = new[] { "ws-1" },
            WindowGeometry = new WindowGeometry(120, 80, 1440, 900),
        };
        var json = JsonSerializer.Serialize(state, CoveJsonContext.Default.CoveState);
        Assert.Contains("\"schemaVersion\"", json);
        Assert.Contains("\"focusedBay\"", json);
        Assert.Contains("\"openBays\"", json);
        Assert.Contains("\"windowGeometry\"", json);
        Assert.Contains("\n", json);
    }

    [Fact]
    public void CoveState_OmitsNullMembers()
    {
        var state = new CoveState();
        var json = JsonSerializer.Serialize(state, CoveJsonContext.Default.CoveState);
        Assert.DoesNotContain("focusedBay", json);
        Assert.DoesNotContain("windowGeometry", json);
    }

    [Fact]
    public void DataDirMeta_SerializesCamelCaseKeys()
    {
        var meta = new DataDirMeta(1, 1751500800000L, "0.1.0");
        var json = JsonSerializer.Serialize(meta, PlatformJsonContext.Default.DataDirMeta);
        Assert.Contains("\"dataDirSchemaVersion\"", json);
        Assert.Contains("\"createdAtUnixMs\"", json);
        Assert.Contains("\"coveVersionAtCreate\"", json);
    }
}
