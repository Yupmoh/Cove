namespace Cove.Protocol.Tests;

internal static class ProtocolVectors
{
    public const string HelloRequestJson = "{\"id\":\"1\",\"uri\":\"cove://sys/hello\",\"params\":{\"protocolVersion\":1,\"clientKind\":\"cli\",\"clientVersion\":\"0.1.0\",\"channel\":\"stable\"}}";
    public const string HelloResponseJson = "{\"id\":\"1\",\"ok\":true,\"data\":{\"protocolVersion\":1,\"engineVersion\":\"0.1.0\",\"enginePid\":12345,\"channel\":\"stable\"}}";
    public const string NookListRequestJson = "{\"id\":\"1\",\"uri\":\"cove://commands/nook.list\"}";
    public const string NookListResponseJson = "{\"id\":\"1\",\"ok\":true,\"data\":{\"nooks\":[]}}";
    public const string NotFoundResponseJson = "{\"id\":\"1\",\"ok\":false,\"error\":{\"code\":\"not_found\",\"message\":\"no nook 7f3a\"}}";

    private static readonly byte[] NookListRequestFrameBytes = HexUtil.Bytes(
        "43 4f 56 45 01 01 00 00 00 00 00 00 00 00 00 00 01 00 00 00 2c 00 00 00" +
        "7b 22 69 64 22 3a 22 31 22 2c 22 75 72 69 22 3a 22 63 6f 76 65 3a 2f 2f" +
        "63 6f 6d 6d 61 6e 64 73 2f 6e 6f 6f 6b 2e 6c 69 73 74 22 7d");

    private static readonly byte[] NookListResponseFrameBytes = HexUtil.Bytes(
        "43 4f 56 45 01 02 00 00 00 00 00 00 00 00 00 00 01 00 00 00 28 00 00 00" +
        "7b 22 69 64 22 3a 22 31 22 2c 22 6f 6b 22 3a 74 72 75 65 2c 22 64 61 74" +
        "61 22 3a 7b 22 6e 6f 6f 6b 73 22 3a 5b 5d 7d 7d");

    private static readonly byte[] StreamDataFrameBytes = HexUtil.Bytes(
        "43 4f 56 45 01 05 00 00 01 00 00 00 00 00 00 00 0c 00 00 00 0d 00 00 00" +
        "04 00 00 00 00 00 00 00 62 79 65 0d 0a");

    private static readonly byte[] CreditFrameBytes = HexUtil.Bytes(
        "43 4f 56 45 01 06 00 00 01 00 00 00 00 00 00 00 14 00 00 00 08 00 00 00" +
        "00 00 02 00 00 00 00 00");

    private static readonly byte[] ResyncFrameBytes = HexUtil.Bytes(
        "43 4f 56 45 01 07 00 00 01 00 00 00 00 00 00 00 90 01 00 00 08 00 00 00" +
        "00 00 90 00 00 00 00 00");

    private static readonly byte[] StreamEndFrameBytes = HexUtil.Bytes(
        "43 4f 56 45 01 08 00 00 01 00 00 00 00 00 00 00 91 01 00 00 0c 00 00 00" +
        "04 02 90 00 00 00 00 00 00 00 00 00");

    private static readonly byte[] ModernResyncPayloadBytes = HexUtil.Bytes(
        "08 07 06 05 04 03 02 01 84 00 00 00 2b 00 00 00 08 00 00 00" +
        "1b 5b 3f 31 30 30 36 68 00 41 ff 1b 5b 48");

    private static readonly byte[] LegacyResyncPayloadBytes = HexUtil.Bytes(
        "08 07 06 05 04 03 02 01 1b 5b 3f 31 30 30 36 68 1b 5b 6d");

    private static readonly byte[] ZeroModeResyncPayloadBytes = HexUtil.Bytes(
        "08 07 06 05 04 03 02 01 50 00 00 00 18 00 00 00 00 00 00 00" +
        "00 7f 80 ff");

    private static readonly byte[] BoundaryModeResyncPayloadBytes = HexUtil.Bytes(
        "08 07 06 05 04 03 02 01 c8 00 00 00 3c 00 00 00 05 00 00 00" +
        "1b 5b 30 6d ff");

    private static readonly byte[] NegativeModeLengthResyncPayloadBytes = HexUtil.Bytes(
        "08 07 06 05 04 03 02 01 50 00 00 00 18 00 00 00 ff ff ff ff");

    private static readonly byte[] OnePastModeLengthResyncPayloadBytes = HexUtil.Bytes(
        "08 07 06 05 04 03 02 01 50 00 00 00 18 00 00 00 04 00 00 00" +
        "1b 5b 6d");

    public static ReadOnlySpan<byte> NookListRequestFrame => NookListRequestFrameBytes;
    public static ReadOnlySpan<byte> NookListResponseFrame => NookListResponseFrameBytes;
    public static ReadOnlySpan<byte> StreamDataFrame => StreamDataFrameBytes;
    public static ReadOnlySpan<byte> CreditFrame => CreditFrameBytes;
    public static ReadOnlySpan<byte> ResyncFrame => ResyncFrameBytes;
    public static ReadOnlySpan<byte> StreamEndFrame => StreamEndFrameBytes;
    public static ReadOnlySpan<byte> StreamDataPayload => StreamDataFrameBytes.AsSpan(ProtocolConstants.HeaderSize);
    public static ReadOnlySpan<byte> ModernResyncPayload => ModernResyncPayloadBytes;
    public static ReadOnlySpan<byte> LegacyResyncPayload => LegacyResyncPayloadBytes;
    public static ReadOnlySpan<byte> ZeroModeResyncPayload => ZeroModeResyncPayloadBytes;
    public static ReadOnlySpan<byte> BoundaryModeResyncPayload => BoundaryModeResyncPayloadBytes;
    public static ReadOnlySpan<byte> NegativeModeLengthResyncPayload => NegativeModeLengthResyncPayloadBytes;
    public static ReadOnlySpan<byte> OnePastModeLengthResyncPayload => OnePastModeLengthResyncPayloadBytes;
}
