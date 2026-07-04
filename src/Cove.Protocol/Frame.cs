namespace Cove.Protocol;

public readonly struct Frame
{
    public Frame(FrameHeader header, byte[] payload)
    {
        Header = header;
        Payload = payload;
    }

    public FrameHeader Header { get; }
    public byte[] Payload { get; }
}
