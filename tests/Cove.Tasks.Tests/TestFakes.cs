using Cove.Tasks.Contracts;

namespace Cove.Tasks.Tests;

public sealed class FakeLaunchProfileResolver : ILaunchProfileResolver
{
    public LaunchProfileResolution? Result { get; set; } = new("claude", "default", "claude --resume", new Dictionary<string, string>());
    public LaunchProfileResolution? ResolveTaskProfile(string ws, string cardId) => Result;
}

public sealed class FakeWorktreeService : IWorktreeService
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

public sealed class FakePaneHost : IPaneHost
{
    public bool ShouldFailCreate { get; set; }
    public bool ShouldFailEnv { get; set; }
    public int CreateCalls { get; private set; }
    public Dictionary<string, Dictionary<string, string>> InjectedEnvs { get; } = new();
    public Dictionary<string, string> BoundCards { get; } = new();
    public PaneCreationResult? CreatePane(string? adapter, int cols, int rows)
    {
        CreateCalls++;
        return ShouldFailCreate ? null : new PaneCreationResult($"pane-{CreateCalls}");
    }
    public bool InjectEnv(string paneId, IReadOnlyDictionary<string, string> env)
    {
        if (ShouldFailEnv) return false;
        InjectedEnvs[paneId] = new Dictionary<string, string>(env);
        return true;
    }
    public bool BindTaskCard(string paneId, string cardId) { BoundCards[paneId] = cardId; return true; }
}

public sealed class FakeRoomService : IRoomService
{
    public List<string> CreatedRoomNames { get; } = new();
    public RoomCreationResult? CreateRoom(string ws, string name, string? parent)
    {
        CreatedRoomNames.Add(name);
        return new RoomCreationResult($"room-{CreatedRoomNames.Count}");
    }
}

public sealed class FakeAgentLauncher : IAgentLauncher
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

public sealed class FakeAdapterResumeLauncher : IAdapterResumeLauncher
{
    public bool ShouldFail { get; set; }
    public string? Error { get; set; }
    public int ResumeCalls { get; private set; }
    public string? LastPriorSessionId { get; private set; }
    public string? LastAdapter { get; set; }
    public AdapterResumeResult Resume(string paneId, string adapter, string resolvedCommand, string priorAdapterSessionId, IReadOnlyDictionary<string, string> env)
    {
        ResumeCalls++;
        LastPriorSessionId = priorAdapterSessionId;
        return ShouldFail ? new AdapterResumeResult("", false, Error ?? "resume failed") : new AdapterResumeResult($"resumed-session-{ResumeCalls}", true, null);
    }
}
