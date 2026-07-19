using Cove.Persistence;
using Cove.Tasks.Contracts;
using Cove.Tasks.Dispatch;
using Cove.Tasks.Runs;
using Cove.Tasks.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class DispatchSagaTests : TasksTestBase
{
    private System.Threading.Tasks.Task<TaskService> NewSvcAsync() => CreateTaskServiceAsync("cove-dispatch-");

    private sealed class FakeProfileResolver : ILaunchProfileResolver
    {
        public LaunchProfileResolution? Result { get; set; } = new("claude", "default", "claude --resume", new Dictionary<string, string>());
        public LaunchProfileResolution? ResolveTaskProfile(string ws, string cardId) => Result;
    }

    private sealed class FakeWorktreeService : IWorktreeService
    {
        public bool ShouldFail { get; set; }
        public int CreateCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public WorktreeCreationResult? CreateAsync(string ws, string branchSource, string branch, string? mergeTarget)
        {
            CreateCalls++;
            return ShouldFail ? null : new WorktreeCreationResult(branch, "/tmp/fake-worktree");
        }
        public bool RemoveAsync(string ws, string branchName) { RemoveCalls++; return true; }
    }

    private sealed class FakeNookHost : INookHost
    {
        public bool ShouldFail { get; set; }
        public bool ShouldFailEnv { get; set; }
        public bool ShouldFailBind { get; set; }
        public int CreateCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public List<string>? CompensationOrder { get; init; }
        public Dictionary<string, Dictionary<string, string>> InjectedEnvs { get; } = new();
        public Dictionary<string, string> BoundCards { get; } = new();
        public NookCreationResult? CreateNook(string? adapter, int cols, int rows)
        {
            CreateCalls++;
            return ShouldFail ? null : new NookCreationResult($"nook-{CreateCalls}");
        }
        public bool InjectEnv(string nookId, IReadOnlyDictionary<string, string> env)
        {
            if (ShouldFailEnv) return false;
            InjectedEnvs[nookId] = new Dictionary<string, string>(env);
            return true;
        }
        public bool BindTaskCard(string nookId, string cardId)
        {
            if (ShouldFailBind) return false;
            BoundCards[nookId] = cardId;
            return true;
        }
        public bool RemoveNook(string nookId)
        {
            RemoveCalls++;
            CompensationOrder?.Add("nook");
            BoundCards.Remove(nookId);
            InjectedEnvs.Remove(nookId);
            return true;
        }
    }

    private sealed class FakeShoreService : IShoreService
    {
        public bool ShouldFail { get; set; }
        public int RemoveCalls { get; private set; }
        public List<string>? CompensationOrder { get; init; }
        public List<string> CreatedShoreNames { get; } = new();
        public ShoreCreationResult? CreateShore(string ws, string name, string? parent)
        {
            if (ShouldFail) return null;
            CreatedShoreNames.Add(name);
            return new ShoreCreationResult($"shore-{CreatedShoreNames.Count}");
        }
        public bool RemoveShore(string bayId, string shoreId)
        {
            RemoveCalls++;
            CompensationOrder?.Add("shore");
            return true;
        }
    }

    private sealed class FakeAgentLauncher : IAgentLauncher
    {
        public bool ShouldFail { get; set; }
        public string? Error { get; set; }
        public int LaunchCalls { get; private set; }
        public int StopCalls { get; private set; }
        public List<string>? CompensationOrder { get; init; }
        public string? LastPrompt { get; private set; }
        public AdapterLaunchResult Launch(string nookId, string adapter, string cmd, IReadOnlyDictionary<string, string> env, string prompt)
        {
            LaunchCalls++;
            LastPrompt = prompt;
            return ShouldFail ? new AdapterLaunchResult("", false, Error ?? "launch failed") : new AdapterLaunchResult($"session-{LaunchCalls}", true, null);
        }
        public bool Stop(string adapterSessionId)
        {
            StopCalls++;
            CompensationOrder?.Add("adapter");
            return true;
        }
    }

    private static async System.Threading.Tasks.Task<string> SeedCardAsync(TaskService svc, string ws = "ws1")
    {
        return (await svc.CreateCardAsync(ws, "test card", "user:test", "do the thing", 1, 2, null)).Id;
    }

    [Fact]
    public async System.Threading.Tasks.Task HappyPath_LaunchesAndMovesStatus()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardAsync(svc);
        var resolver = new FakeProfileResolver();
        var worktree = new FakeWorktreeService();
        var nookHost = new FakeNookHost();
        var shores = new FakeShoreService();
        var launcher = new FakeAgentLauncher();
        var saga = new DispatchSaga(svc, resolver, worktree, nookHost, shores, launcher, NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", null);

        Assert.True(result.Success, result.Error);
        Assert.Equal(DispatchStep.Completed, result.ReachedStep);
        Assert.NotNull(result.RunId);
        Assert.Equal(1, nookHost.CreateCalls);
        Assert.Equal(1, launcher.LaunchCalls);
        Assert.Contains("COVE_TASK_ID", nookHost.InjectedEnvs["nook-1"].Keys);
        Assert.Contains("COVE_TASK_RUN_ID", nookHost.InjectedEnvs["nook-1"].Keys);
        Assert.Equal(cardId, nookHost.BoundCards["nook-1"]);
        var card = svc.GetCard(cardId);
        Assert.Equal("in-progress", card!.StatusId);
        Assert.Equal(result.RunId, card.CurrentPrimaryRunId);
    }

    [Fact]
    public async System.Threading.Tasks.Task ProfileResolutionFails_ReturnsEarly()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardAsync(svc);
        var resolver = new FakeProfileResolver { Result = null };
        var saga = new DispatchSaga(svc, resolver, new FakeWorktreeService(), new FakeNookHost(), new FakeShoreService(), new FakeAgentLauncher(), NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", null);

        Assert.False(result.Success);
        Assert.Equal(DispatchStep.ResolveConfig, result.ReachedStep);
        Assert.Empty(svc.ListRuns(cardId, null, null));
    }

    [Fact]
    public async System.Threading.Tasks.Task WorktreeCreationFails_CompensatesRun()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardAsync(svc);
        await svc.SetLaunchConfigAsync(cardId, new LaunchConfig.LaunchConfigModel
        {
            Adapter = "claude",
            ExecutionMode = "worktree",
            WorktreeBranchSource = "task",
            WorktreeBranchName = "COVE-1",
        }, new LaunchConfig.LaunchConfigValidationContext(
            new HashSet<string> { "claude" },
            new HashSet<string> { "todo", "in-progress" },
            new HashSet<string> { "default" }));
        var resolver = new FakeProfileResolver();
        var worktree = new FakeWorktreeService { ShouldFail = true };
        var saga = new DispatchSaga(svc, resolver, worktree, new FakeNookHost(), new FakeShoreService(), new FakeAgentLauncher(), NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", "worktree");

        Assert.False(result.Success);
        Assert.Equal(DispatchStep.CreateWorktree, result.ReachedStep);
        var runs = svc.ListRuns(cardId, null, null);
        Assert.Single(runs);
        Assert.Equal("cancelled", runs[0].State);
    }

    [Fact]
    public async System.Threading.Tasks.Task NookCreationFails_CompensatesRunAndWorktree()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardAsync(svc);
        var resolver = new FakeProfileResolver();
        var worktree = new FakeWorktreeService();
        var nookHost = new FakeNookHost { ShouldFail = true };
        var saga = new DispatchSaga(svc, resolver, worktree, nookHost, new FakeShoreService(), new FakeAgentLauncher(), NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", "worktree");

        Assert.False(result.Success);
        Assert.Equal(DispatchStep.EnsureNook, result.ReachedStep);
        Assert.Equal(1, worktree.RemoveCalls);
        var runs = svc.ListRuns(cardId, null, null);
        Assert.Equal("cancelled", runs[0].State);
    }

    [Fact]
    public async System.Threading.Tasks.Task AdapterLaunchFails_CompensatesRun()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardAsync(svc);
        var resolver = new FakeProfileResolver();
        var worktree = new FakeWorktreeService();
        var nook = new FakeNookHost();
        var shore = new FakeShoreService();
        var launcher = new FakeAgentLauncher { ShouldFail = true, Error = "adapter crashed" };
        var saga = new DispatchSaga(svc, resolver, worktree, nook, shore, launcher, NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", "worktree");

        Assert.False(result.Success);
        Assert.Equal(DispatchStep.LaunchAdapter, result.ReachedStep);
        Assert.Contains("adapter crashed", result.Error);
        Assert.Equal(1, shore.RemoveCalls);
        Assert.Equal(1, nook.RemoveCalls);
        Assert.Equal(1, worktree.RemoveCalls);
        var runs = svc.ListRuns(cardId, null, null);
        Assert.Equal("cancelled", runs[0].State);
    }

    [Fact]
    public async System.Threading.Tasks.Task ShoreCreationFails_CompensatesNookWorktreeAndRun()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardAsync(svc);
        var card = svc.GetCard(cardId)!;
        card.LaunchConfigJson = LaunchConfig.LaunchConfigSerializer.Serialize(new LaunchConfig.LaunchConfigModel
        {
            Adapter = "claude",
            ExecutionMode = "worktree",
        });
        await svc.UpdateCardAsync(card);
        var worktree = new FakeWorktreeService();
        var nook = new FakeNookHost();
        var shore = new FakeShoreService { ShouldFail = true };
        var saga = new DispatchSaga(svc, new FakeProfileResolver(), worktree, nook, shore, new FakeAgentLauncher(), NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", "worktree");

        Assert.False(result.Success);
        Assert.Equal(DispatchStep.EnsureShore, result.ReachedStep);
        Assert.Equal(1, nook.RemoveCalls);
        Assert.Equal(1, worktree.RemoveCalls);
        Assert.Equal("cancelled", Assert.Single(svc.ListRuns(cardId, null, null)).State);
    }

    [Theory]
    [InlineData(true, false, DispatchStep.InjectEnv)]
    [InlineData(false, true, DispatchStep.BindCard)]
    public async System.Threading.Tasks.Task NookSetupFailure_CompensatesNookAndRun(
        bool failEnv,
        bool failBind,
        DispatchStep expectedStep)
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardAsync(svc);
        var nook = new FakeNookHost { ShouldFailEnv = failEnv, ShouldFailBind = failBind };
        var saga = new DispatchSaga(svc, new FakeProfileResolver(), new FakeWorktreeService(), nook, new FakeShoreService(), new FakeAgentLauncher(), NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", null);

        Assert.False(result.Success);
        Assert.Equal(expectedStep, result.ReachedStep);
        Assert.Equal(1, nook.RemoveCalls);
        Assert.Equal("cancelled", Assert.Single(svc.ListRuns(cardId, null, null)).State);
    }

    [Fact]
    public async System.Threading.Tasks.Task DurableCompletionFailure_CompensatesInStrictReverseAcquisitionOrder()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardAsync(svc);
        var card = svc.GetCard(cardId)!;
        card.LaunchConfigJson = LaunchConfig.LaunchConfigSerializer.Serialize(new LaunchConfig.LaunchConfigModel
        {
            Adapter = "claude",
            ExecutionMode = "worktree",
            InProgressStatusId = "missing-status",
        });
        await svc.UpdateCardAsync(card);
        var compensationOrder = new List<string>();
        var worktree = new FakeWorktreeServiceWithOrder(compensationOrder);
        var nook = new FakeNookHost { CompensationOrder = compensationOrder };
        var shore = new FakeShoreService { CompensationOrder = compensationOrder };
        var launcher = new FakeAgentLauncher { CompensationOrder = compensationOrder };
        var saga = new DispatchSaga(svc, new FakeProfileResolver(), worktree, nook, shore, launcher, NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", "worktree");

        Assert.False(result.Success);
        Assert.Equal(DispatchStep.MoveStatus, result.ReachedStep);
        Assert.Equal(new[] { "adapter", "shore", "nook", "worktree" }, compensationOrder);
        Assert.Equal("todo", svc.GetCard(cardId)!.StatusId);
        Assert.Empty(svc.ListRunSegments(result.RunId!));
        Assert.Equal("cancelled", Assert.Single(svc.ListRuns(cardId, null, null)).State);
    }

    private sealed class FakeWorktreeServiceWithOrder(List<string> compensationOrder) : IWorktreeService
    {
        public WorktreeCreationResult? CreateAsync(string bayId, string branchSource, string branch, string? mergeTarget)
            => new(branch, "/tmp/fake-worktree");

        public bool RemoveAsync(string bayId, string branchName)
        {
            compensationOrder.Add("worktree");
            return true;
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task WorktreeMode_CreatesAutoNamedShore()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardAsync(svc);
        await svc.SetLaunchConfigAsync(cardId, new LaunchConfig.LaunchConfigModel
        {
            Adapter = "claude",
            ExecutionMode = "worktree",
            WorktreeBranchSource = "task",
            WorktreeBranchName = "COVE-1",
        }, new LaunchConfig.LaunchConfigValidationContext(
            new HashSet<string> { "claude" },
            new HashSet<string> { "todo", "in-progress" },
            new HashSet<string> { "default" }));
        var resolver = new FakeProfileResolver();
        var shores = new FakeShoreService();
        var saga = new DispatchSaga(svc, resolver, new FakeWorktreeService(), new FakeNookHost(), shores, new FakeAgentLauncher(), NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", "worktree");

        Assert.True(result.Success);
        Assert.Single(shores.CreatedShoreNames);
        Assert.Contains("COVE-1 - test card", shores.CreatedShoreNames[0]);
    }

    [Fact]
    public async System.Threading.Tasks.Task HappyPath_PromptContainsTitleAndDescription()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardAsync(svc);
        var launcher = new FakeAgentLauncher();
        var saga = new DispatchSaga(svc, new FakeProfileResolver(), new FakeWorktreeService(), new FakeNookHost(), new FakeShoreService(), launcher, NullLogger.Instance);

        await saga.LaunchAsync(cardId, "ws1", null);

        Assert.NotNull(launcher.LastPrompt);
        Assert.Contains("test card", launcher.LastPrompt);
        Assert.Contains("do the thing", launcher.LastPrompt);
    }
}
