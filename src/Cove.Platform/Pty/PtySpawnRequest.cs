using System.Collections.Generic;

namespace Cove.Platform.Pty;

public sealed class PtySpawnRequest
{
    public required string Command { get; init; }
    public required IReadOnlyList<string> Args { get; init; }
    public string? WorkingDirectory { get; init; }
    public IReadOnlyDictionary<string, string>? Environment { get; init; }
    public int Cols { get; init; } = PtyConstants.DefaultCols;
    public int Rows { get; init; } = PtyConstants.DefaultRows;
}
