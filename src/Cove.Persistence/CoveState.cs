namespace Cove.Persistence;

public sealed record CoveState
{
    private readonly int _schemaVersion = 1;
    public int SchemaVersion { get => _schemaVersion == 0 ? 1 : _schemaVersion; init => _schemaVersion = value; }
    public string? FocusedBay { get; init; }
    private readonly IReadOnlyList<string>? _openBays;
    public IReadOnlyList<string> OpenBays { get => _openBays ?? Array.Empty<string>(); init => _openBays = value; }
    public WindowGeometry? WindowGeometry { get; init; }
    public bool CleanShutdown { get; init; }
    public DateTimeOffset? ShutdownAtUtc { get; init; }
    public bool AutoRestoreOnLaunch { get; init; }
}

public sealed record WindowGeometry(double X, double Y, double Width, double Height);
