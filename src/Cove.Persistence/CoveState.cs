namespace Cove.Persistence;

public sealed record CoveState
{
    private readonly int _schemaVersion = 1;
    public int SchemaVersion { get => _schemaVersion == 0 ? 1 : _schemaVersion; init => _schemaVersion = value; }
    public string? FocusedWorkspace { get; init; }
    private readonly IReadOnlyList<string>? _openWorkspaces;
    public IReadOnlyList<string> OpenWorkspaces { get => _openWorkspaces ?? Array.Empty<string>(); init => _openWorkspaces = value; }
    public WindowGeometry? WindowGeometry { get; init; }
    public bool CleanShutdown { get; init; }
    public DateTimeOffset? ShutdownAtUtc { get; init; }
    public bool AutoRestoreOnLaunch { get; init; }
}

public sealed record WindowGeometry(double X, double Y, double Width, double Height);
