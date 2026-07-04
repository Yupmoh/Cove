namespace Cove.Protocol;

public static class ProtocolConstants
{
    public static ReadOnlySpan<byte> Magic => "COVE"u8;

    public const byte WireVersion = 1;
    public const int SemanticProtocolVersion = 1;

    public const int HeaderSize = 24;
    public const int MaxFramePayload = 16 * 1024 * 1024;
    public const int StreamDataMaxRawBytes = 64 * 1024;
    public const int PtyReadBuffer = 64 * 1024;

    public const int FlowWindow = 256 * 1024;
    public const int CreditReplenishThreshold = 128 * 1024;
    public const int RingCapacityDefault = 8 * 1024 * 1024;

    public const int ControlRequestTimeoutMs = 30_000;
    public const int ReadinessTimeoutMs = 5_000;
    public const int SpawnPollMs = 25;
    public const int IdleExitSeconds = 300;
}

public enum FrameType : byte
{
    Invalid = 0x00,
    Request = 0x01,
    Response = 0x02,
    Event = 0x03,
    Error = 0x04,
    StreamData = 0x05,
    Credit = 0x06,
    Resync = 0x07,
    StreamEnd = 0x08,
}
