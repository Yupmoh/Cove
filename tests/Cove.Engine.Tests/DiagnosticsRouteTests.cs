using System.Text.Json;
using Cove.Engine.Diagnostics;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class DiagnosticsRouteTests
{
    private static EngineDispatchContext Ctx(string uri, DiagnosticsHub? hub, JsonElement? paramsEl = null)
    {
        var request = new ControlRequest("1", uri, paramsEl);
        return new EngineDispatchContext(request, diagnostics: hub);
    }

    [Fact]
    public async Task Status_ReturnsEnabledAndConfig()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, true, 42, System.TimeSpan.FromMinutes(2)));
        var resp = await DiagnosticsCommands.Status(Ctx("cove://commands/diagnostics.status", hub));

        Assert.True(resp.Ok);
        var dto = resp.Data!.Value.Deserialize(CoveJsonContext.Default.DiagnosticsStatusResult)!;
        Assert.True(dto.Enabled);
        Assert.True(dto.WebInspectorOptIn);
        Assert.Equal(42, dto.MaxSnapshots);
        Assert.Equal(120, dto.SnapshotIntervalSeconds);
    }

    [Fact]
    public async Task Status_NoHub_Fails()
    {
        var resp = await DiagnosticsCommands.Status(Ctx("cove://commands/diagnostics.status", hub: null));
        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }

    [Fact]
    public async Task SnapshotTake_ReturnsSnapshotJson()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, System.TimeSpan.FromMinutes(5)));
        var p = JsonSerializer.SerializeToElement(new DiagnosticsSnapshotTakeParams(3, 1, 2), CoveJsonContext.Default.DiagnosticsSnapshotTakeParams);
        var resp = await DiagnosticsCommands.SnapshotTake(Ctx("cove://commands/diagnostics.snapshot.take", hub, p));

        Assert.True(resp.Ok);
        Assert.Equal(3, resp.Data!.Value.GetProperty("activeNooks").GetInt32());
        Assert.Equal(1, resp.Data!.Value.GetProperty("activeBays").GetInt32());
        Assert.Equal(2, resp.Data!.Value.GetProperty("activeAgents").GetInt32());
    }

    [Fact]
    public async Task SnapshotTake_NoParams_UsesDefaults()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, System.TimeSpan.FromMinutes(5)));
        var resp = await DiagnosticsCommands.SnapshotTake(Ctx("cove://commands/diagnostics.snapshot.take", hub));

        Assert.True(resp.Ok);
        Assert.Equal(0, resp.Data!.Value.GetProperty("activeNooks").GetInt32());
    }

    [Fact]
    public async Task SnapshotTake_NoHub_Fails()
    {
        var resp = await DiagnosticsCommands.SnapshotTake(Ctx("cove://commands/diagnostics.snapshot.take", hub: null));
        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }

    [Fact]
    public async Task SnapshotList_ReturnsStoredSnapshots()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, System.TimeSpan.FromMinutes(5)));
        hub.TakeSnapshot();
        hub.TakeSnapshot();
        var resp = await DiagnosticsCommands.SnapshotList(Ctx("cove://commands/diagnostics.snapshot.list", hub));

        Assert.True(resp.Ok);
        Assert.Equal(JsonValueKind.Array, resp.Data!.Value.ValueKind);
        Assert.Equal(2, resp.Data!.Value.GetArrayLength());
    }

    [Fact]
    public async Task SnapshotList_NoHub_Fails()
    {
        var resp = await DiagnosticsCommands.SnapshotList(Ctx("cove://commands/diagnostics.snapshot.list", hub: null));
        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }

    [Fact]
    public async Task Export_NoPath_ReturnsPayload()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, System.TimeSpan.FromMinutes(5)));
        hub.TakeSnapshot();
        var resp = await DiagnosticsCommands.Export(Ctx("cove://commands/diagnostics.export", hub));

        Assert.True(resp.Ok);
        Assert.Equal(JsonValueKind.Array, resp.Data!.Value.ValueKind);
        Assert.Equal(1, resp.Data!.Value.GetArrayLength());
    }

    [Fact]
    public async Task Export_WithPath_WritesFileAndReturnsPath()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, System.TimeSpan.FromMinutes(5)));
        hub.TakeSnapshot();
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-diag-{System.Guid.NewGuid():N}", "export.json");
        var p = JsonSerializer.SerializeToElement(new DiagnosticsExportParams(path), CoveJsonContext.Default.DiagnosticsExportParams);
        var resp = await DiagnosticsCommands.Export(Ctx("cove://commands/diagnostics.export", hub, p));

        Assert.True(resp.Ok);
        var dto = resp.Data!.Value.Deserialize(CoveJsonContext.Default.DiagnosticsExportResult)!;
        Assert.Equal(path, dto.Path);
        Assert.True(System.IO.File.Exists(path));
    }

    [Fact]
    public async Task Export_NoHub_Fails()
    {
        var resp = await DiagnosticsCommands.Export(Ctx("cove://commands/diagnostics.export", hub: null));
        Assert.False(resp.Ok);
        Assert.Equal("not_ready", resp.Error!.Code);
    }
}
