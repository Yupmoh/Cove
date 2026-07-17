using System.Diagnostics;
using System.Text;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class RegistryHandoffTests
{
    private static void WaitForRingText(NookRegistry registry, string nookId, string marker)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < 15)
        {
            if (registry.Search(nookId, marker, caseSensitive: true).Length > 0)
                return;
            Thread.Sleep(20);
        }
        Assert.Fail($"marker '{marker}' never appeared in nook {nookId}");
    }

    [Fact]
    public void ExportThenAdopt_KeepsProcessHistoryAndOffsets()
    {
        if (OperatingSystem.IsWindows()) return;
        string shell = File.Exists("/bin/zsh") ? "/bin/zsh" : "/bin/bash";
        var logger = NullLogger.Instance;
        var predecessor = new NookRegistry(PtyHostFactory.Create(logger), logger);
        var info = predecessor.Spawn(new SpawnParams(shell, new[] { "-i" }, "/tmp", null, 80, 24, null, null, null, null, null));
        var nookId = info.NookId;
        predecessor.Write(nookId, Encoding.UTF8.GetBytes("printf 'HANDOFF_%s\\n' BEFORE\n"));
        WaitForRingText(predecessor, nookId, "HANDOFF_BEFORE");

        var items = predecessor.ExportForHandoff();
        var item = Assert.Single(items);
        Assert.Equal(nookId, item.Record.NookId);
        Assert.True(item.Record.RingHead > 0);
        Assert.Equal(item.RingTail.Length, item.Record.RingLength);

        var successor = new NookRegistry(PtyHostFactory.Create(logger), logger);
        var adopted = successor.Adopt(item.Record, item.MasterFd, item.RingTail);
        Assert.NotNull(adopted);
        Assert.Equal(nookId, adopted!.NookId);
        Assert.True(successor.ConsumePendingRepaint(nookId));
        Assert.False(successor.ConsumePendingRepaint(nookId));


        Assert.True(successor.Search(nookId, "HANDOFF_BEFORE", caseSensitive: true).Length > 0);

        successor.Write(nookId, Encoding.UTF8.GetBytes("printf 'HANDOFF_%s\\n' AFTER\n"));
        WaitForRingText(successor, nookId, "HANDOFF_AFTER");

        Assert.True(successor.Kill(nookId));
    }

    [Fact]
    public void Adopt_DeadPid_IsRejectedWithoutThrowing()
    {
        if (OperatingSystem.IsWindows()) return;
        var logger = NullLogger.Instance;
        var successor = new NookRegistry(PtyHostFactory.Create(logger), logger);
        var record = new HandoffNookRecord("nook-dead", 99999999, "/bin/zsh", new[] { "-i" }, "/tmp", null, 80, 24, null, null, null, 0, 0, null, null, null);
        var (a, b) = Cove.Platform.Pty.Unix.UnixFdChannel.CreateSocketPair();
        try
        {
            Assert.Null(successor.Adopt(record, a, Array.Empty<byte>()));
        }
        finally
        {
            Cove.Platform.Pty.Unix.UnixFdChannel.CloseFd(a);
            Cove.Platform.Pty.Unix.UnixFdChannel.CloseFd(b);
        }
    }
}
