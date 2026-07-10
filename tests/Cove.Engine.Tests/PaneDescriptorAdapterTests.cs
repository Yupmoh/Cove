using System.IO;
using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class PaneDescriptorAdapterTests
{
    [Fact]
    public void PaneDescriptor_RoundTripsAdapterIdentity()
    {
        var d = new PaneDescriptor("p1", "claude", System.Array.Empty<string>(), "/repo", "Claude Code", "claude-code", "Claude Code", "sess-abc");
        var json = JsonSerializer.Serialize(d, CoveJsonContext.Default.PaneDescriptor);
        var back = JsonSerializer.Deserialize(json, CoveJsonContext.Default.PaneDescriptor)!;
        Assert.Equal("claude-code", back.Adapter);
        Assert.Equal("Claude Code", back.AgentName);
        Assert.Equal("sess-abc", back.SessionId);
    }

    [Fact]
    public void PaneDescriptor_AbsentAdapterMembers_DefaultToNull()
    {
        var json = "{\"paneId\":\"p1\",\"command\":\"/bin/sh\",\"args\":[],\"cwd\":\"/tmp\"}";
        var back = JsonSerializer.Deserialize(json, CoveJsonContext.Default.PaneDescriptor)!;
        Assert.Null(back.Adapter);
        Assert.Null(back.AgentName);
        Assert.Null(back.SessionId);
    }

    [Fact]
    public void WorkspacePersistence_PreservesAdapterIdentity()
    {
        var wsDir = Path.Combine(Path.GetTempPath(), "covews-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var leaf = new PaneLeaf { PaneId = "p1", Subtabs = new[] { new Subtab("p1", PaneType.Terminal) } };
            var ws = new WorkspaceSnapshot
            {
                Id = "w1",
                Name = "w1",
                ProjectDir = "/proj",
                ActiveRoomId = "r1",
                Rooms = new[] { new RoomSnapshot { Id = "r1", Name = "room", LayoutTree = leaf, ZoomedPaneId = null } },
            };
            var descs = new[] { new PaneDescriptor("p1", "claude", System.Array.Empty<string>(), "/repo", null, "claude-code", "Claude Code", "sess-1") };

            WorkspacePersistence.Save(ws, descs, wsDir);
            var (_, sessions) = WorkspacePersistence.Load(wsDir, NullLogger.Instance);

            Assert.Equal("claude-code", sessions["p1"].Adapter);
            Assert.Equal("Claude Code", sessions["p1"].AgentName);
            Assert.Equal("sess-1", sessions["p1"].SessionId);
        }
        finally { try { Directory.Delete(wsDir, true); } catch { } }
    }
}
