using System.Text.Json;
using Cove.Protocol;
using Xunit;

namespace Cove.ClientContract.Tests;

public sealed class ClientContractTests
{
    [Theory]
    [InlineData(ClientFlavor.Cli)]
    [InlineData(ClientFlavor.Gui)]
    [InlineData(ClientFlavor.Tui)]
    public async Task Control_clients_share_handshake_request_and_success_contract(ClientFlavor flavor)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var observation = await ExerciseControlContractAsync(flavor, success: true, timeout.Token);

        Assert.True(observation.Ok);
        Assert.True(observation.Data!.Value.GetProperty("accepted").GetBoolean());
        Assert.Null(observation.ErrorCode);
        Assert.Null(observation.ErrorMessage);
    }

    [Theory]
    [InlineData(ClientFlavor.Cli)]
    [InlineData(ClientFlavor.Gui)]
    [InlineData(ClientFlavor.Tui)]
    public async Task Control_clients_share_handshake_request_and_error_contract(ClientFlavor flavor)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var observation = await ExerciseControlContractAsync(flavor, success: false, timeout.Token);

        Assert.False(observation.Ok);
        Assert.Null(observation.Data);
        Assert.Equal("contract-denied", observation.ErrorCode);
        Assert.Equal("contract request denied", observation.ErrorMessage);
    }

    [Theory]
    [InlineData(ClientFlavor.Cli)]
    [InlineData(ClientFlavor.Gui)]
    [InlineData(ClientFlavor.Tui)]
    public async Task Stream_clients_share_subscribe_data_credit_resync_checkpoint_and_end_contract(ClientFlavor flavor)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var transport = await LoopbackTransport.CreateAsync(timeout.Token);
        await using var driver = ClientContractDriver.Create(flavor, transport.ClientStream, NewRoot());
        var server = ServeStreamContractAsync(
            transport.ServerStream,
            driver.StreamClientKind,
            driver.Source,
            timeout.Token);

        var observation = await driver.ObserveStreamAsync(timeout.Token);
        var credits = await server;

        Assert.Equal(ContractVectors.Subscription(), observation.Subscription);
        Assert.Equal(2, observation.Data.Count);
        Assert.Equal(ContractVectors.BaseOffset, observation.Data[0].Offset);
        Assert.Equal(ContractVectors.FirstChunk, observation.Data[0].Data);
        Assert.Equal(ContractVectors.ResyncOffset, observation.Data[1].Offset);
        Assert.Equal(ContractVectors.SecondChunk, observation.Data[1].Data);
        var resync = Assert.Single(observation.Resyncs);
        Assert.Equal(ContractVectors.ResyncOffset, resync.Offset);
        Assert.Equal(ContractVectors.ResyncModes, resync.Modes);
        Assert.Equal(ContractVectors.ResyncCheckpoint, resync.Checkpoint);
        Assert.Equal(132, resync.Cols);
        Assert.Equal(40, resync.Rows);
        Assert.Equal(new EndObservation(203, 17), observation.End);
        Assert.Equal(new ulong[] { 103, 203 }, credits);
    }

    [Fact]
    public async Task Tui_production_surface_matches_canonical_stream_contract()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var transport = await LoopbackTransport.CreateAsync(timeout.Token);
        await using var driver = ClientContractDriver.Create(
            ClientFlavor.Tui,
            transport.ClientStream,
            NewRoot());
        var server = ServeStreamContractAsync(
            transport.ServerStream,
            driver.StreamClientKind,
            driver.Source,
            timeout.Token);

        var observation = await driver.ObserveStreamAsync(timeout.Token);
        var credits = await server;

        Assert.IsType<TuiClientContractDriver>(driver);
        Assert.Equal("tui-attach", driver.StreamClientKind);
        Assert.Equal(ContractVectors.Subscription(), observation.Subscription);
        Assert.Equal(2, observation.Data.Count);
        Assert.Equal(ContractVectors.BaseOffset, observation.Data[0].Offset);
        Assert.Equal(ContractVectors.FirstChunk, observation.Data[0].Data);
        Assert.Equal(ContractVectors.ResyncOffset, observation.Data[1].Offset);
        Assert.Equal(ContractVectors.SecondChunk, observation.Data[1].Data);
        var resync = Assert.Single(observation.Resyncs);
        Assert.Equal(ContractVectors.ResyncOffset, resync.Offset);
        Assert.Equal(ContractVectors.ResyncModes, resync.Modes);
        Assert.Equal(ContractVectors.ResyncCheckpoint, resync.Checkpoint);
        Assert.Equal(132, resync.Cols);
        Assert.Equal(40, resync.Rows);
        Assert.Equal(new EndObservation(203, 17), observation.End);
        Assert.Equal(new ulong[] { 103, 203 }, credits);
    }

    [Theory]
    [MemberData(nameof(TuiFailureVectors))]
    public async Task Tui_production_surface_rejects_bad_or_incomplete_frames_without_hanging(
        byte[]? bytes,
        string expectedError)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var transport = await LoopbackTransport.CreateAsync(timeout.Token);
        await using var driver = ClientContractDriver.Create(
            ClientFlavor.Tui,
            transport.ClientStream,
            NewRoot());
        Assert.IsType<TuiClientContractDriver>(driver);
        var server = ServeTuiFailureAsync(transport.ServerStream, bytes, timeout.Token);

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => driver.ObserveStreamAsync(timeout.Token));
        await server;

        Assert.Contains(expectedError, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public static TheoryData<byte[]?, string> TuiFailureVectors => new()
    {
        { ContractVectors.MalformedFrame, "malformed_frame" },
        { ContractVectors.TruncatedFrame, "mid-frame" },
        { null, "without completion" }
    };

    private static async Task ServeControlContractAsync(
        Stream stream,
        string expectedClientKind,
        string expectedSource,
        bool success,
        CancellationToken cancellationToken)
    {
        await using var connection = new FrameConnection(stream);
        await AcceptHelloAsync(connection, expectedClientKind, cancellationToken).ConfigureAwait(false);

        var request = await ReadRequestAsync(connection, cancellationToken).ConfigureAwait(false);
        Assert.Equal(expectedSource, request.Source);
        if (success)
        {
            Assert.Equal("cove://commands/nook.list", request.Uri);
            Assert.Null(request.Params);
            await connection.WriteFrameAsync(
                FrameType.Response,
                0,
                ControlCodec.Encode(new ControlResponse(request.Id, true, ContractVectors.CommandResult())),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Assert.Equal("cove://commands/nook.list", request.Uri);
            Assert.Null(request.Params);
            await connection.WriteFrameAsync(
                FrameType.Response,
                0,
                ControlCodec.Encode(new ControlResponse(
                    request.Id,
                    false,
                    Error: new ControlError("contract-denied", "contract request denied"))),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<ControlObservation> ExerciseControlContractAsync(
        ClientFlavor flavor,
        bool success,
        CancellationToken cancellationToken)
    {
        if (flavor == ClientFlavor.Cli)
        {
            await using var transport = CliControlTransport.Create();
            await using var driver = ClientContractDriver.Create(flavor, Stream.Null, transport.Root);
            var server = ServeAcceptedControlContractAsync(
                transport,
                driver.ControlClientKind,
                driver.Source,
                success,
                cancellationToken);
            var observation = await driver.RequestAsync(
                "cove://commands/nook.list",
                null,
                cancellationToken).ConfigureAwait(false);
            await server.ConfigureAwait(false);
            return observation;
        }

        await using var loopback = await LoopbackTransport.CreateAsync(cancellationToken).ConfigureAwait(false);
        await using var loopbackDriver = ClientContractDriver.Create(flavor, loopback.ClientStream, NewRoot());
        var loopbackServer = ServeControlContractAsync(
            loopback.ServerStream,
            loopbackDriver.ControlClientKind,
            loopbackDriver.Source,
            success,
            cancellationToken);
        var loopbackObservation = await loopbackDriver.RequestAsync(
            "cove://commands/nook.list",
            null,
            cancellationToken).ConfigureAwait(false);
        await loopbackServer.ConfigureAwait(false);
        return loopbackObservation;
    }

    private static async Task ServeAcceptedControlContractAsync(
        CliControlTransport transport,
        string expectedClientKind,
        string expectedSource,
        bool success,
        CancellationToken cancellationToken)
    {
        await using var stream = await transport.AcceptAsync(cancellationToken).ConfigureAwait(false);
        await ServeControlContractAsync(
            stream,
            expectedClientKind,
            expectedSource,
            success,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ulong>> ServeStreamContractAsync(
        Stream stream,
        string expectedClientKind,
        string expectedSource,
        CancellationToken cancellationToken)
    {
        await using var connection = new FrameConnection(stream);
        await AcceptHelloAsync(connection, expectedClientKind, cancellationToken).ConfigureAwait(false);
        var subscribeRequest = await ReadRequestAsync(connection, cancellationToken).ConfigureAwait(false);
        Assert.Equal("cove://commands/nook.subscribe", subscribeRequest.Uri);
        Assert.Equal(expectedSource, subscribeRequest.Source);
        var subscribe = JsonSerializer.Deserialize(subscribeRequest.Params!.Value, CoveJsonContext.Default.SubscribeParams);
        Assert.Equal(ContractVectors.NookId, subscribe!.NookId);
        var subscription = ContractVectors.Subscription();
        var result = JsonSerializer.SerializeToElement(subscription, CoveJsonContext.Default.SubscribeResult);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(subscribeRequest.Id, true, result)),
            cancellationToken).ConfigureAwait(false);

        var credits = new List<ulong>();
        await SendDataAsync(connection, ContractVectors.BaseOffset, ContractVectors.FirstChunk, cancellationToken).ConfigureAwait(false);
        credits.Add(await ReadCreditAsync(connection, cancellationToken).ConfigureAwait(false));

        var resyncPayload = new byte[
            StreamPayload.ResyncHeaderSize
            + ContractVectors.ResyncModes.Length
            + ContractVectors.ResyncCheckpoint.Length];
        StreamPayload.WriteResync(
            resyncPayload,
            ContractVectors.ResyncOffset,
            132,
            40,
            ContractVectors.ResyncModes,
            ContractVectors.ResyncCheckpoint);
        await connection.WriteFrameAsync(
            FrameType.Resync,
            ContractVectors.StreamId,
            resyncPayload,
            cancellationToken).ConfigureAwait(false);

        await SendDataAsync(connection, ContractVectors.ResyncOffset, ContractVectors.SecondChunk, cancellationToken).ConfigureAwait(false);
        credits.Add(await ReadCreditAsync(connection, cancellationToken).ConfigureAwait(false));

        var endPayload = new byte[12];
        StreamPayload.WriteStreamEnd(endPayload, 203, 17);
        await connection.WriteFrameAsync(
            FrameType.StreamEnd,
            ContractVectors.StreamId,
            endPayload,
            cancellationToken).ConfigureAwait(false);
        return credits;
    }

    private static async Task ServeTuiFailureAsync(
        Stream stream,
        byte[]? bytes,
        CancellationToken cancellationToken)
    {
        await using var connection = new FrameConnection(stream);
        await AcceptHelloAsync(connection, "tui-attach", cancellationToken).ConfigureAwait(false);
        var subscribeRequest = await ReadRequestAsync(connection, cancellationToken).ConfigureAwait(false);
        Assert.Equal("cove://commands/nook.subscribe", subscribeRequest.Uri);
        Assert.Equal("user:tui", subscribeRequest.Source);
        var result = JsonSerializer.SerializeToElement(
            ContractVectors.Subscription(),
            CoveJsonContext.Default.SubscribeResult);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(subscribeRequest.Id, true, result)),
            cancellationToken).ConfigureAwait(false);
        if (bytes is not null)
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        stream.Dispose();
    }

    private static async Task AcceptHelloAsync(
        FrameConnection connection,
        string expectedClientKind,
        CancellationToken cancellationToken)
    {
        var request = await ReadRequestAsync(connection, cancellationToken).ConfigureAwait(false);
        Assert.Equal("cove://sys/hello", request.Uri);
        Assert.Null(request.Source);
        var hello = JsonSerializer.Deserialize(request.Params!.Value, CoveJsonContext.Default.HelloParams);
        Assert.NotNull(hello);
        Assert.Equal(ProtocolConstants.SemanticProtocolVersion, hello.ProtocolVersion);
        Assert.Equal(expectedClientKind, hello.ClientKind);
        Assert.False(string.IsNullOrWhiteSpace(hello.ClientVersion));
        Assert.Equal(ContractVectors.Channel, hello.Channel);
        var data = JsonSerializer.SerializeToElement(
            new HelloResult(
                ProtocolConstants.SemanticProtocolVersion,
                "contract-engine",
                Environment.ProcessId,
                ContractVectors.Channel),
            CoveJsonContext.Default.HelloResult);
        await connection.WriteFrameAsync(
            FrameType.Response,
            0,
            ControlCodec.Encode(new ControlResponse(request.Id, true, data)),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ControlRequest> ReadRequestAsync(
        FrameConnection connection,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var frame = await connection.ReadFrameAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new IOException("client closed the contract connection");
            if (frame.Header.Type == FrameType.Request)
                return ControlCodec.DecodeRequest(frame.Payload);
        }
    }

    private static Task SendDataAsync(
        FrameConnection connection,
        ulong offset,
        byte[] data,
        CancellationToken cancellationToken)
    {
        var payload = new byte[8 + data.Length];
        StreamPayload.WriteStreamData(payload, offset, data);
        return connection.WriteFrameAsync(
            FrameType.StreamData,
            ContractVectors.StreamId,
            payload,
            cancellationToken).AsTask();
    }

    private static async Task<ulong> ReadCreditAsync(
        FrameConnection connection,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var frame = await connection.ReadFrameAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new IOException("client closed before credit");
            if (frame.Header.Type != FrameType.Credit)
                continue;
            Assert.Equal(ContractVectors.StreamId, frame.Header.StreamId);
            return StreamPayload.ReadOffset(frame.Payload);
        }
    }

    private static string NewRoot() => Path.Combine(
        Path.GetTempPath(),
        "cove-client-contract-" + Guid.NewGuid().ToString("N"));
}
