using Cove.Tasks.Contracts;

namespace Cove.Engine.Tests;

public sealed class AcceptanceFakeProfileResolver : ILaunchProfileResolver
{
    public LaunchProfileResolution? Result { get; set; } = new("claude", "default", "claude --resume", new Dictionary<string, string>());
    public LaunchProfileResolution? ResolveTaskProfile(string ws, string cardId) => Result;
}

public sealed class AcceptanceFakeWorktreeService : IWorktreeService
{
    public WorktreeCreationResult? CreateAsync(string ws, string branchSource, string branch, string? mergeTarget) => new(branch, "/tmp/fake-wt");
    public bool RemoveAsync(string ws, string branchName) => true;
}

public sealed class AcceptanceFakePaneHost : IPaneHost
{
    public Dictionary<string, Dictionary<string, string>> InjectedEnvs { get; } = new();
    public Dictionary<string, string> BoundCards { get; } = new();
    public PaneCreationResult? CreatePane(string? adapter, int cols, int rows) => new("pane-1");
    public bool InjectEnv(string paneId, IReadOnlyDictionary<string, string> env) { InjectedEnvs[paneId] = new Dictionary<string, string>(env); return true; }
    public bool BindTaskCard(string paneId, string cardId) { BoundCards[paneId] = cardId; return true; }
}

public sealed class AcceptanceFakeRoomService : IRoomService
{
    public RoomCreationResult? CreateRoom(string ws, string name, string? parent) => new("room-1");
}

public sealed class AcceptanceFakeAgentLauncher : IAgentLauncher
{
    public AdapterLaunchResult Launch(string paneId, string adapter, string cmd, IReadOnlyDictionary<string, string> env, string prompt) => new("session-1", true, null);
}

public sealed class AcceptanceFakeResumeLauncher : IAdapterResumeLauncher
{
    public AdapterResumeResult Resume(string paneId, string adapter, string cmd, string priorSessionId, IReadOnlyDictionary<string, string> env) => new("resumed-1", true, null);
}
