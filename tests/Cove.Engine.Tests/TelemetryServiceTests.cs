using Cove.Engine.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class TelemetryServiceTests
{
    private static string NewDir()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-telem-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void DisabledByDefault_DatabaseDoesNotExist()
    {
        var svc = new TelemetryService(NewDir(), NullLogger.Instance);
        Assert.False(svc.IsEnabled);
        Assert.False(svc.DatabaseExists);
        Assert.False(svc.DeviceIdExists);
    }

    [Fact]
    public void Enabling_CreatesDatabaseAndDeviceId()
    {
        var dir = NewDir();
        var svc = new TelemetryService(dir, NullLogger.Instance);
        svc.IsEnabled = true;
        Assert.True(svc.DatabaseExists);
        Assert.True(svc.DeviceIdExists);
        Assert.NotNull(svc.DeviceId);
    }

    [Fact]
    public void Record_WhenDisabled_DoesNothing()
    {
        var svc = new TelemetryService(NewDir(), NullLogger.Instance);
        svc.Record("lifecycle", "launch", new Dictionary<string, object> { ["version"] = "1.0.0" });
        Assert.False(svc.DatabaseExists);
    }

    [Fact]
    public void Record_WhenEnabled_PersistsEvent()
    {
        var svc = new TelemetryService(NewDir(), NullLogger.Instance);
        svc.IsEnabled = true;
        svc.Record("lifecycle", "launch", new Dictionary<string, object> { ["version"] = "1.0.0", ["count"] = 1 });

        var events = svc.GetQueuedEvents();
        Assert.Single(events);
        Assert.Equal("lifecycle", events[0].Category);
        Assert.Equal("launch", events[0].Name);
        Assert.Contains("1.0.0", events[0].PayloadJson);
    }

    [Fact]
    public void Record_NonPrimitiveValue_Throws()
    {
        var svc = new TelemetryService(NewDir(), NullLogger.Instance);
        svc.IsEnabled = true;
        var badPayload = new Dictionary<string, object> { ["scrollback"] = new[] { "line1", "line2" } };
        Assert.Throws<System.InvalidOperationException>(() => svc.Record("test", "bad", badPayload));
    }
    [Fact]
    public void Record_UnknownField_Throws()
    {
        var svc = new TelemetryService(NewDir(), NullLogger.Instance);
        svc.IsEnabled = true;
        var badPayload = new Dictionary<string, object> { ["filePath"] = "/secret/path" };
        Assert.Throws<System.InvalidOperationException>(() => svc.Record("test", "bad", badPayload));
    }

    [Fact]
    public void Record_ScrollbackField_Throws()
    {
        var svc = new TelemetryService(NewDir(), NullLogger.Instance);
        svc.IsEnabled = true;
        var badPayload = new Dictionary<string, object> { ["scrollback"] = "some content" };
        Assert.Throws<System.InvalidOperationException>(() => svc.Record("test", "bad", badPayload));
    }

    [Fact]
    public void Record_PromptField_Throws()
    {
        var svc = new TelemetryService(NewDir(), NullLogger.Instance);
        svc.IsEnabled = true;
        var badPayload = new Dictionary<string, object> { ["prompt"] = "user prompt text" };
        Assert.Throws<System.InvalidOperationException>(() => svc.Record("test", "bad", badPayload));
    }

    [Fact]
    public void Record_OverlengthString_Throws()
    {
        var svc = new TelemetryService(NewDir(), NullLogger.Instance);
        svc.IsEnabled = true;
        var longValue = new string('x', 300);
        var badPayload = new Dictionary<string, object> { ["command"] = longValue };
        Assert.Throws<System.InvalidOperationException>(() => svc.Record("test", "bad", badPayload));
    }

    [Fact]
    public void Record_AllowedField_Accepted()
    {
        var svc = new TelemetryService(NewDir(), NullLogger.Instance);
        svc.IsEnabled = true;
        svc.Record("lifecycle", "command", new Dictionary<string, object> { ["command"] = "git-status", ["exitCode"] = 0, ["duration"] = 1.5 });
        Assert.Single(svc.GetQueuedEvents());
    }


    [Fact]
    public void DeviceId_PersistsAcrossInstances()
    {
        var dir = NewDir();
        var svc1 = new TelemetryService(dir, NullLogger.Instance);
        svc1.IsEnabled = true;
        var id1 = svc1.DeviceId;

        var svc2 = new TelemetryService(dir, NullLogger.Instance);
        svc2.IsEnabled = true;
        Assert.Equal(id1, svc2.DeviceId);
    }

    [Fact]
    public void Record_StringValue_AcceptedAsPrimitive()
    {
        var svc = new TelemetryService(NewDir(), NullLogger.Instance);
        svc.IsEnabled = true;
        svc.Record("test", "event", new Dictionary<string, object> { ["command"] = "some-string" });
        Assert.Single(svc.GetQueuedEvents());
    }
    [Fact]
    public void ClearQueue_RemovesAllEvents()
    {
        var svc = new TelemetryService(NewDir(), NullLogger.Instance);
        svc.IsEnabled = true;
        svc.Record("test", "event1", new Dictionary<string, object> { ["count"] = 1 });
        svc.Record("test", "event2", new Dictionary<string, object> { ["count"] = 2 });
        Assert.Equal(2, svc.GetQueuedEvents().Count);

        svc.ClearQueue();
        Assert.Empty(svc.GetQueuedEvents());
    }

    [Fact]
    public void GetQueuedEvents_ReturnsMostRecentFirst()
    {
        var svc = new TelemetryService(NewDir(), NullLogger.Instance);
        svc.IsEnabled = true;
        svc.Record("test", "first", new Dictionary<string, object> { ["count"] = 1 });
        System.Threading.Thread.Sleep(10);
        svc.Record("test", "second", new Dictionary<string, object> { ["count"] = 2 });

        var events = svc.GetQueuedEvents();
        Assert.Equal("second", events[0].Name);
        Assert.Equal("first", events[1].Name);
    }

    [Fact]
    public void SchemaRejects_NestedObjects()
    {
        var svc = new TelemetryService(NewDir(), NullLogger.Instance);
        svc.IsEnabled = true;
        var badPayload = new Dictionary<string, object> { ["config"] = new { secret = "value" } };
        Assert.Throws<System.InvalidOperationException>(() => svc.Record("test", "bad", badPayload));
    }
}
