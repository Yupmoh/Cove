using System.IO;
using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookDescriptorAdapterTests
{
    [Fact]
    public void NookDescriptor_RoundTripsAdapterIdentity()
    {
        var d = new NookDescriptor("p1", "claude", System.Array.Empty<string>(), "/repo", "Claude Code", "claude-code", "Claude Code", "sess-abc");
        var json = JsonSerializer.Serialize(d, CoveJsonContext.Default.NookDescriptor);
        var back = JsonSerializer.Deserialize(json, CoveJsonContext.Default.NookDescriptor)!;
        Assert.Equal("claude-code", back.Adapter);
        Assert.Equal("Claude Code", back.AgentName);
        Assert.Equal("sess-abc", back.SessionId);
    }

    [Fact]
    public void NookDescriptor_AbsentAdapterMembers_DefaultToNull()
    {
        var json = "{\"nookId\":\"p1\",\"command\":\"/bin/sh\",\"args\":[],\"cwd\":\"/tmp\"}";
        var back = JsonSerializer.Deserialize(json, CoveJsonContext.Default.NookDescriptor)!;
        Assert.Null(back.Adapter);
        Assert.Null(back.AgentName);
        Assert.Null(back.SessionId);
    }

    [Fact]
    public void BayPersistence_PreservesAdapterIdentity()
    {
        var wsDir = Path.Combine(Path.GetTempPath(), "covews-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var leaf = new NookLeaf { NookId = "p1", Subtabs = new[] { new Subtab("p1", NookType.Terminal) } };
            var ws = new BaySnapshot
            {
                Id = "w1",
                Name = "w1",
                ProjectDir = "/proj",
                ActiveShoreId = "r1",
                Shores = new[] { new ShoreSnapshot { Id = "r1", Name = "shore", LayoutTree = leaf, ZoomedNookId = null } },
            };
            var descs = new[] { new NookDescriptor("p1", "claude", System.Array.Empty<string>(), "/repo", null, "claude-code", "Claude Code", "sess-1") };

            BayPersistence.Save(ws, descs, wsDir);
            var (_, sessions) = BayPersistence.Load(wsDir, NullLogger.Instance);

            Assert.Equal("claude-code", sessions["p1"].Adapter);
            Assert.Equal("Claude Code", sessions["p1"].AgentName);
            Assert.Equal("sess-1", sessions["p1"].SessionId);
        }
        finally { try { Directory.Delete(wsDir, true); } catch { } }
    }
}
