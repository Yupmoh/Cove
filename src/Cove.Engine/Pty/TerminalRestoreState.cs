namespace Cove.Engine.Pty;

public sealed record TerminalRestoreState(
    byte[] Checkpoint,
    byte[] Tail,
    long Offset,
    int Cols,
    int Rows,
    int ScrollbackLines,
    string ModeSupplement = "");

public sealed record TerminalResyncSnapshot(
 long Offset,
 byte[] Checkpoint,
 int Cols,
 int Rows,
 string ModePreamble);
