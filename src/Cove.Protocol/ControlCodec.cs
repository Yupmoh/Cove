using System.Text.Json;

namespace Cove.Protocol;

public static class ControlCodec
{
    public static byte[] Encode(ControlRequest value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, CoveJsonContext.Default.ControlRequest);

    public static byte[] Encode(ControlResponse value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, CoveJsonContext.Default.ControlResponse);

    public static byte[] Encode(ControlEvent value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, CoveJsonContext.Default.ControlEvent);

    public static byte[] Encode(ControlErrorFrame value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, CoveJsonContext.Default.ControlErrorFrame);

    public static ControlRequest DecodeRequest(ReadOnlySpan<byte> payload) =>
        JsonSerializer.Deserialize(payload, CoveJsonContext.Default.ControlRequest)
        ?? throw new ProtocolException("malformed_frame", "request json deserialized to null");

    public static ControlResponse DecodeResponse(ReadOnlySpan<byte> payload) =>
        JsonSerializer.Deserialize(payload, CoveJsonContext.Default.ControlResponse)
        ?? throw new ProtocolException("malformed_frame", "response json deserialized to null");

    public static ControlEvent DecodeEvent(ReadOnlySpan<byte> payload) =>
        JsonSerializer.Deserialize(payload, CoveJsonContext.Default.ControlEvent)
        ?? throw new ProtocolException("malformed_frame", "event json deserialized to null");

    public static ControlErrorFrame DecodeErrorFrame(ReadOnlySpan<byte> payload) =>
        JsonSerializer.Deserialize(payload, CoveJsonContext.Default.ControlErrorFrame)
        ?? throw new ProtocolException("malformed_frame", "error json deserialized to null");
}
