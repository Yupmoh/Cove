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

public sealed class FakeNookHost : INookHost
{
    public bool ShouldFailCreate { get; set; }
    public bool ShouldFailEnv { get; set; }
    public int CreateCalls { get; private set; }
    public Dictionary<string, Dictionary<string, string>> InjectedEnvs { get; } = new();
    public Dictionary<string, string> BoundCards { get; } = new();
    public NookCreationResult? CreateNook(string? adapter, int cols, int rows)
    {
        CreateCalls++;
        return ShouldFailCreate ? null : new NookCreationResult($"nook-{CreateCalls}");
    }
    public bool InjectEnv(string nookId, IReadOnlyDictionary<string, string> env)
    {
        if (ShouldFailEnv) return false;
        InjectedEnvs[nookId] = new Dictionary<string, string>(env);
        return true;
    }
    public bool BindTaskCard(string nookId, string cardId) { BoundCards[nookId] = cardId; return true; }
}

public sealed class FakeShoreService : IShoreService
{
    public List<string> CreatedShoreNames { get; } = new();
    public ShoreCreationResult? CreateShore(string ws, string name, string? parent)
    {
        CreatedShoreNames.Add(name);
        return new ShoreCreationResult($"shore-{CreatedShoreNames.Count}");
    }
}

public sealed class FakeAgentLauncher : IAgentLauncher
{
    public bool ShouldFail { get; set; }
    public string? Error { get; set; }
    public int LaunchCalls { get; private set; }
    public string? LastPrompt { get; private set; }
    public AdapterLaunchResult Launch(string nookId, string adapter, string cmd, IReadOnlyDictionary<string, string> env, string prompt)
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
    public AdapterResumeResult Resume(string nookId, string adapter, string resolvedCommand, string priorAdapterSessionId, IReadOnlyDictionary<string, string> env)
    {
        ResumeCalls++;
        LastPriorSessionId = priorAdapterSessionId;
        return ShouldFail ? new AdapterResumeResult("", false, Error ?? "resume failed") : new AdapterResumeResult($"resumed-session-{ResumeCalls}", true, null);
    }
}
