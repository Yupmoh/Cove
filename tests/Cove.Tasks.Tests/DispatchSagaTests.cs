using Cove.Persistence;
using Cove.Tasks.Contracts;
using Cove.Tasks.Dispatch;
using Cove.Tasks.Runs;
using Cove.Tasks.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Tasks.Tests;

public sealed class DispatchSagaTests
{
    private static async System.Threading.Tasks.Task<TaskService> NewSvcAsync()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-dispatch-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var svc = new TaskService(dir, NullLogger.Instance);
        await svc.StartAsync();
        return svc;
    }

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

    private sealed class FakePaneHost : IPaneHost
    {
        public bool ShouldFail { get; set; }
        public int CreateCalls { get; private set; }
        public Dictionary<string, Dictionary<string, string>> InjectedEnvs { get; } = new();
        public Dictionary<string, string> BoundCards { get; } = new();
        public PaneCreationResult? CreatePane(string? adapter, int cols, int rows)
        {
            CreateCalls++;
            return ShouldFail ? null : new PaneCreationResult($"pane-{CreateCalls}");
        }
        public bool InjectEnv(string paneId, IReadOnlyDictionary<string, string> env)
        {
            InjectedEnvs[paneId] = new Dictionary<string, string>(env);
            return true;
        }
        public bool BindTaskCard(string paneId, string cardId) { BoundCards[paneId] = cardId; return true; }
    }

    private sealed class FakeRoomService : IRoomService
    {
        public List<string> CreatedRoomNames { get; } = new();
        public RoomCreationResult? CreateRoom(string ws, string name, string? parent)
        {
            CreatedRoomNames.Add(name);
            return new RoomCreationResult($"room-{CreatedRoomNames.Count}");
        }
    }

    private sealed class FakeAgentLauncher : IAgentLauncher
    {
        public bool ShouldFail { get; set; }
        public string? Error { get; set; }
        public int LaunchCalls { get; private set; }
        public string? LastPrompt { get; private set; }
        public AdapterLaunchResult Launch(string paneId, string adapter, string cmd, IReadOnlyDictionary<string, string> env, string prompt)
        {
            LaunchCalls++;
            LastPrompt = prompt;
            return ShouldFail ? new AdapterLaunchResult("", false, Error ?? "launch failed") : new AdapterLaunchResult($"session-{LaunchCalls}", true, null);
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
        var paneHost = new FakePaneHost();
        var rooms = new FakeRoomService();
        var launcher = new FakeAgentLauncher();
        var saga = new DispatchSaga(svc, resolver, worktree, paneHost, rooms, launcher, NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", null);

        Assert.True(result.Success, result.Error);
        Assert.Equal(DispatchStep.Completed, result.ReachedStep);
        Assert.NotNull(result.RunId);
        Assert.Equal(1, paneHost.CreateCalls);
        Assert.Equal(1, launcher.LaunchCalls);
        Assert.Contains("COVE_TASK_ID", paneHost.InjectedEnvs["pane-1"].Keys);
        Assert.Contains("COVE_TASK_RUN_ID", paneHost.InjectedEnvs["pane-1"].Keys);
        Assert.Equal(cardId, paneHost.BoundCards["pane-1"]);
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
        var saga = new DispatchSaga(svc, resolver, new FakeWorktreeService(), new FakePaneHost(), new FakeRoomService(), new FakeAgentLauncher(), NullLogger.Instance);

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
        var saga = new DispatchSaga(svc, resolver, worktree, new FakePaneHost(), new FakeRoomService(), new FakeAgentLauncher(), NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", "worktree");

        Assert.False(result.Success);
        Assert.Equal(DispatchStep.CreateWorktree, result.ReachedStep);
        var runs = svc.ListRuns(cardId, null, null);
        Assert.Single(runs);
        Assert.Equal("cancelled", runs[0].State);
    }

    [Fact]
    public async System.Threading.Tasks.Task PaneCreationFails_CompensatesRunAndWorktree()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardAsync(svc);
        var resolver = new FakeProfileResolver();
        var worktree = new FakeWorktreeService();
        var paneHost = new FakePaneHost { ShouldFail = true };
        var saga = new DispatchSaga(svc, resolver, worktree, paneHost, new FakeRoomService(), new FakeAgentLauncher(), NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", null);

        Assert.False(result.Success);
        Assert.Equal(DispatchStep.EnsurePane, result.ReachedStep);
        var runs = svc.ListRuns(cardId, null, null);
        Assert.Equal("cancelled", runs[0].State);
    }

    [Fact]
    public async System.Threading.Tasks.Task AdapterLaunchFails_CompensatesRun()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardAsync(svc);
        var resolver = new FakeProfileResolver();
        var launcher = new FakeAgentLauncher { ShouldFail = true, Error = "adapter crashed" };
        var saga = new DispatchSaga(svc, resolver, new FakeWorktreeService(), new FakePaneHost(), new FakeRoomService(), launcher, NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", null);

        Assert.False(result.Success);
        Assert.Equal(DispatchStep.LaunchAdapter, result.ReachedStep);
        Assert.Contains("adapter crashed", result.Error);
        var runs = svc.ListRuns(cardId, null, null);
        Assert.Equal("cancelled", runs[0].State);
    }

    [Fact]
    public async System.Threading.Tasks.Task WorktreeMode_CreatesAutoNamedRoom()
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
        var rooms = new FakeRoomService();
        var saga = new DispatchSaga(svc, resolver, new FakeWorktreeService(), new FakePaneHost(), rooms, new FakeAgentLauncher(), NullLogger.Instance);

        var result = await saga.LaunchAsync(cardId, "ws1", "worktree");

        Assert.True(result.Success);
        Assert.Single(rooms.CreatedRoomNames);
        Assert.Contains("COVE-1 - test card", rooms.CreatedRoomNames[0]);
    }

    [Fact]
    public async System.Threading.Tasks.Task HappyPath_PromptContainsTitleAndDescription()
    {
        var svc = await NewSvcAsync();
        var cardId = await SeedCardAsync(svc);
        var launcher = new FakeAgentLauncher();
        var saga = new DispatchSaga(svc, new FakeProfileResolver(), new FakeWorktreeService(), new FakePaneHost(), new FakeRoomService(), launcher, NullLogger.Instance);

        await saga.LaunchAsync(cardId, "ws1", null);

        Assert.NotNull(launcher.LastPrompt);
        Assert.Contains("test card", launcher.LastPrompt);
        Assert.Contains("do the thing", launcher.LastPrompt);
    }
}
