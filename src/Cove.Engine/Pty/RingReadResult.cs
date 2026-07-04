namespace Cove.Engine.Pty;

public readonly struct RingReadResult
{
    public int BytesCopied { get; init; }
    public long NextOffset { get; init; }
    public bool Underrun { get; init; }
}
