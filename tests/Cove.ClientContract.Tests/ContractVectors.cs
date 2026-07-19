using System.Text;
using System.Text.Json;
using Cove.Protocol;

namespace Cove.ClientContract.Tests;

public enum ClientFlavor
{
    Cli,
    Gui,
    Tui
}

internal static class ContractVectors
{
    public const string Channel = "dev";
    public const string NookId = "nook-contract";
    public const ulong StreamId = 41;
    public const ulong BaseOffset = 100;
    public const ulong ResyncOffset = 200;
    public static readonly byte[] InitialModes = Encoding.ASCII.GetBytes("\u001b[?2004h");
    public static readonly byte[] InitialCheckpoint = Encoding.UTF8.GetBytes("initial-checkpoint");
    public static readonly byte[] ResyncModes = Encoding.ASCII.GetBytes("\u001b[?1006h");
    public static readonly byte[] ResyncCheckpoint = Encoding.UTF8.GetBytes("resync-checkpoint");
    public static readonly byte[] FirstChunk = Encoding.UTF8.GetBytes("abc");
    public static readonly byte[] SecondChunk = Encoding.UTF8.GetBytes("xyz");
    public static readonly byte[] MalformedFrame =
    [
        0x4e, 0x4f, 0x50, 0x45, 0x01, 0x05, 0x00, 0x00,
        0x29, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    ];
    public static readonly byte[] TruncatedFrame =
    [
        0x43, 0x4f, 0x56, 0x45, 0x01, 0x05, 0x00, 0x00,
        0x29, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00,
        0x64, 0x00, 0x00, 0x00
    ];

    public static JsonElement CommandResult()
    {
        using var document = JsonDocument.Parse("""{"accepted":true}""");
        return document.RootElement.Clone();
    }

    public static SubscribeResult Subscription() => new(
        StreamId,
        BaseOffset,
        ProtocolConstants.FlowWindow,
        BaseOffset,
        Convert.ToBase64String(InitialModes),
        Convert.ToBase64String(InitialCheckpoint),
        120,
        36);
}

internal sealed record DataObservation(ulong Offset, byte[] Data);
internal sealed record ResyncObservation(ulong Offset, byte[] Modes, byte[] Checkpoint, int Cols, int Rows);
internal sealed record EndObservation(ulong FinalOffset, int ExitCode);
internal sealed record StreamObservation(
    SubscribeResult Subscription,
    IReadOnlyList<DataObservation> Data,
    IReadOnlyList<ResyncObservation> Resyncs,
    EndObservation End);
