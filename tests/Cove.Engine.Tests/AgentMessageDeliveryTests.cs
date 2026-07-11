using System.Text;
using Cove.Engine.Agents;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AgentMessageDeliveryTests
{
    private sealed class CapturingNookRegistry : INookWriter
    {
        public List<byte> Written { get; } = new();
        public List<string> Segments { get; } = new();

        public bool Write(string nookId, ReadOnlySpan<byte> data)
        {
            Written.AddRange(data.ToArray());
            Segments.Add(Encoding.UTF8.GetString(data));
            return true;
        }
    }

    [Fact]
    public async Task Deliver_WrapsBodyInBracketedPasteSequence()
    {
        var registry = new CapturingNookRegistry();
        var delivery = new AgentMessageDelivery(registry);
        var body = "hello world";

        await delivery.DeliverAsync("nook-1", body, submitPauseMs: 0);

        var written = Encoding.UTF8.GetString(registry.Written.ToArray());
        Assert.Contains("\x1b[200~", written);
        Assert.Contains("hello world", written);
        Assert.Contains("\x1b[201~", written);
    }

    [Fact]
    public async Task Deliver_WritesSubmitEnterAfterPause()
    {
        var registry = new CapturingNookRegistry();
        var delivery = new AgentMessageDelivery(registry);

        await delivery.DeliverAsync("nook-1", "body", submitPauseMs: 0);

        var lastSegment = registry.Segments[^1];
        Assert.EndsWith("\r", lastSegment);
    }

    [Fact]
    public async Task Deliver_ObeysCustomSubmitPause()
    {
        var registry = new CapturingNookRegistry();
        var delivery = new AgentMessageDelivery(registry);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await delivery.DeliverAsync("nook-1", "body", submitPauseMs: 100);

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds >= 90, $"expected >=100ms pause, got {sw.ElapsedMilliseconds}");
    }

    [Fact]
    public void Deliver_DefaultSubmitPause_Is250Ms()
    {
        Assert.Equal(250, AgentMessageDelivery.DefaultSubmitPauseMs);
    }
}
