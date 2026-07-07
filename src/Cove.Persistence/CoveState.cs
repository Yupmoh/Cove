namespace Cove.Persistence;

public sealed record CoveState
{
    public int SchemaVersion { get; init; } = 1;
    public string? FocusedWorkspace { get; init; }
    public IReadOnlyList<string> OpenWorkspaces { get; init; } = Array.Empty<string>();
    public WindowGeometry? WindowGeometry { get; init; }
    public bool CleanShutdown { get; init; }
    public DateTimeOffset? ShutdownAtUtc { get; init; }
    public bool AutoRestoreOnLaunch { get; init; }
}

public sealed record WindowGeometry(double X, double Y, double Width, double Height);
