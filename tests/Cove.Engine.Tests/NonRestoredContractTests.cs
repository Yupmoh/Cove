using Cove.Engine.Restart;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NonRestoredContractTests
{
    [Fact]
    public void NonRestoredSet_DocumentsAllFourCategories()
    {
        var contract = NonRestoredContract.Items;
        Assert.Contains(NonRestoredKind.UnsavedEditorBuffers, contract.Keys);
        Assert.Contains(NonRestoredKind.InFlightForegroundCommands, contract.Keys);
        Assert.Contains(NonRestoredKind.BrowserCookiesAcrossUpgrade, contract.Keys);
        Assert.Contains(NonRestoredKind.ReapedAdapterSessions, contract.Keys);
        Assert.Equal(4, contract.Count);
    }

    [Fact]
    public void NonRestoredSet_ExcludesAdapterResume()
    {
        Assert.DoesNotContain(NonRestoredKind.AdapterSessionResume, NonRestoredContract.Items.Keys);
    }

    [Fact]
    public async Task ReapedAdapterSession_FreshLaunches_NotResumed()
    {
        var adapter = new TestAdapters.ReapedFakeAdapter();
        var svc = new AgentResumeService(adapter);
        var state = await svc.ResumeAsync("sess-1", new LauncherOverrides { Yolo = true, WorkingDir = "/repo" }, CancellationToken.None);

        Assert.Equal(AgentResumeState.Succeeded, state.State);
        Assert.NotNull(state.Command);
        Assert.DoesNotContain("--resume", state.Command!.Args);
        Assert.Contains("--dangerously-skip-permissions", state.Command.Args);
    }

    [Fact]
    public void EachNonRestoredItem_HasDocumentation()
    {
        foreach (var kv in NonRestoredContract.Items)
        {
            Assert.False(string.IsNullOrWhiteSpace(kv.Value));
            Assert.True(kv.Value.Length > 20);
        }
    }
}

internal static class TestAdapters
{
    public sealed class ReapedFakeAdapter : IAdapterResume
    {
        public ResumeCommand BuildResumeCommand(string sessionId, LauncherOverrides overrides)
        {
            var args = new List<string> { "--resume", sessionId };
            if (overrides.Yolo)
                args.Add("--dangerously-skip-permissions");
            return new ResumeCommand("agent", args, overrides.WorkingDir ?? "");
        }

        public Task WaitForReadiness(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;
        public bool IsSessionReaped(string sessionId) => true;
    }
}
