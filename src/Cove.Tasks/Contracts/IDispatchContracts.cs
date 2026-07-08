namespace Cove.Tasks.Contracts;

public sealed record LaunchProfileResolution(string Adapter, string? ProfileSlug, string? ResolvedCommand, System.Collections.Generic.IReadOnlyDictionary<string, string> Env);

public interface ILaunchProfileResolver
{
    LaunchProfileResolution? ResolveTaskProfile(string workspaceId, string cardId);
}

public sealed record WorktreeCreationResult(string BranchName, string Path);

public interface IWorktreeService
{
    WorktreeCreationResult? CreateAsync(string workspaceId, string branchSource, string branch, string? mergeTarget);
    bool RemoveAsync(string workspaceId, string branchName);
}

public sealed record PaneCreationResult(string PaneId);

public interface IPaneHost
{
    PaneCreationResult? CreatePane(string? adapter, int cols, int rows);
    bool InjectEnv(string paneId, System.Collections.Generic.IReadOnlyDictionary<string, string> env);
    bool BindTaskCard(string paneId, string cardId);
}

public sealed record RoomCreationResult(string RoomId);

public interface IRoomService
{
    RoomCreationResult? CreateRoom(string workspaceId, string name, string? parentRoomId);
}

public sealed record AdapterLaunchResult(string AdapterSessionId, bool Success, string? Error);

public interface IAgentLauncher
{
    AdapterLaunchResult Launch(string paneId, string adapter, string resolvedCommand, System.Collections.Generic.IReadOnlyDictionary<string, string> env, string prompt);
}
