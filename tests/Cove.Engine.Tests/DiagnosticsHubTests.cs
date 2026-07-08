using System.Text.Json;
using Cove.Engine.Diagnostics;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class DiagnosticsHubTests
{
    [Fact]
    public void Hub_OffByDefault()
    {
        var hub = new DiagnosticsHub();
        Assert.False(hub.Enabled);
    }

    [Fact]
    public void Enable_TurnsHubOn()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(false, false, 100, TimeSpan.FromMinutes(5)));
        hub.Enable();
        Assert.True(hub.Enabled);
    }

    [Fact]
    public void Disable_TurnsHubOff()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, TimeSpan.FromMinutes(5)));
        hub.Disable();
        Assert.False(hub.Enabled);
    }

    [Fact]
    public void TakeSnapshot_ReturnsCurrentMetrics()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, TimeSpan.FromMinutes(5)));
        var snapshot = hub.TakeSnapshot(activePanes: 3, activeWorkspaces: 1, activeAgents: 2);

        Assert.True(snapshot.ManagedMemoryBytes > 0);
        Assert.True(snapshot.WorkingSetBytes > 0);
        Assert.True(snapshot.ThreadCount > 0);
        Assert.Equal(3, snapshot.ActivePanes);
        Assert.Equal(1, snapshot.ActiveWorkspaces);
        Assert.Equal(2, snapshot.ActiveAgents);
    }

    [Fact]
    public void TakeSnapshot_StoresWhenEnabled()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, TimeSpan.FromMinutes(5)));
        hub.TakeSnapshot();
        hub.TakeSnapshot();
        hub.TakeSnapshot();

        var snapshots = hub.GetSnapshots();
        Assert.Equal(3, snapshots.Count);
    }

    [Fact]
    public void TakeSnapshot_DoesNotStoreWhenDisabled()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(false, false, 100, TimeSpan.FromMinutes(5)));
        hub.TakeSnapshot();
        hub.TakeSnapshot();

        Assert.Empty(hub.GetSnapshots());
    }

    [Fact]
    public void Snapshots_PruneAtMaxCapacity()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 3, TimeSpan.FromMinutes(5)));
        for (var i = 0; i < 5; i++)
            hub.TakeSnapshot();

        var snapshots = hub.GetSnapshots();
        Assert.Equal(3, snapshots.Count);
    }

    [Fact]
    public void Configure_SetsWebInspectorOptIn()
    {
        var hub = new DiagnosticsHub();
        Assert.False(hub.Config.WebInspectorOptIn);

        hub.Configure(new DiagnosticsConfig(true, true, 50, TimeSpan.FromMinutes(1)));
        Assert.True(hub.Config.WebInspectorOptIn);
        Assert.True(hub.Enabled);
        Assert.Equal(50, hub.Config.MaxSnapshots);
    }

    [Fact]
    public void ExportSnapshotJson_ProducesValidJson()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, TimeSpan.FromMinutes(5)));
        var snapshot = hub.TakeSnapshot();

        var json = hub.ExportSnapshotJson(snapshot);
        Assert.False(string.IsNullOrEmpty(json));

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("managedMemoryBytes", out _));
        Assert.True(doc.RootElement.TryGetProperty("workingSetBytes", out _));
        Assert.True(doc.RootElement.TryGetProperty("threadCount", out _));
    }

    [Fact]
    public void ExportAllSnapshotsJson_ProducesArray()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, TimeSpan.FromMinutes(5)));
        hub.TakeSnapshot();
        hub.TakeSnapshot();

        var json = hub.ExportAllSnapshotsJson();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void TakeSnapshot_CapturesPaneScrollbackBytes()
    {
        var hub = new DiagnosticsHub(new DiagnosticsConfig(true, false, 100, TimeSpan.FromMinutes(5)));
        var scrollback = new Dictionary<string, long> { ["pane-1"] = 1024, ["pane-2"] = 2048 };
        var snapshot = hub.TakeSnapshot(paneScrollbackBytes: scrollback);

        Assert.Equal(1024, snapshot.PaneScrollbackBytes["pane-1"]);
        Assert.Equal(2048, snapshot.PaneScrollbackBytes["pane-2"]);
    }
}
