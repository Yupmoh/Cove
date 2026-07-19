namespace Cove.Tasks.Contracts;

public sealed record LaunchProfileResolution(string Adapter, string? ProfileSlug, string? ResolvedCommand, System.Collections.Generic.IReadOnlyDictionary<string, string> Env);

public interface ILaunchProfileResolver
{
    LaunchProfileResolution? ResolveTaskProfile(string bayId, string cardId);
}

public sealed record WorktreeCreationResult(string BranchName, string Path);

public interface IWorktreeService
{
    WorktreeCreationResult? CreateAsync(string bayId, string branchSource, string branch, string? mergeTarget);
    bool RemoveAsync(string bayId, string branchName);
}

public sealed record NookCreationResult(string NookId);

public interface INookHost
{
    NookCreationResult? CreateNook(string? adapter, int cols, int rows);
    bool InjectEnv(string nookId, System.Collections.Generic.IReadOnlyDictionary<string, string> env);
    bool BindTaskCard(string nookId, string cardId);
    bool RemoveNook(string nookId);
}

public sealed record ShoreCreationResult(string ShoreId);

public interface IShoreService
{
    ShoreCreationResult? CreateShore(string bayId, string name, string? parentShoreId);
    bool RemoveShore(string bayId, string shoreId);
}

public sealed record AdapterLaunchResult(string AdapterSessionId, bool Success, string? Error);

public interface IAgentLauncher
{
    AdapterLaunchResult Launch(string nookId, string adapter, string resolvedCommand, System.Collections.Generic.IReadOnlyDictionary<string, string> env, string prompt);
    bool Stop(string adapterSessionId);
}

public sealed record AdapterResumeResult(string AdapterSessionId, bool Success, string? Error);

public interface IAdapterResumeLauncher
{
    AdapterResumeResult Resume(string nookId, string adapter, string resolvedCommand, string priorAdapterSessionId, System.Collections.Generic.IReadOnlyDictionary<string, string> env);
}
