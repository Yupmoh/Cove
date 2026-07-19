using System.Text;
using System.Text.Json;
using Xunit;

namespace Cove.Protocol.Tests;

public sealed class ControlCodecTests
{
    [Fact]
    public void HelloRequest_EncodesAndDecodesCanonicalVector()
    {
        JsonElement parameters = JsonSerializer.SerializeToElement(
            new HelloParams(1, "cli", "0.1.0", "stable"), CoveJsonContext.Default.HelloParams);
        var request = new ControlRequest("1", "cove://sys/hello", parameters);

        byte[] encoded = ControlCodec.Encode(request);
        Assert.Equal(ProtocolVectors.HelloRequestJson, Encoding.UTF8.GetString(encoded));

        ControlRequest decoded = ControlCodec.DecodeRequest(encoded);
        Assert.Equal(request.Id, decoded.Id);
        Assert.Equal(request.Uri, decoded.Uri);
        var hello = decoded.Params!.Value.Deserialize(CoveJsonContext.Default.HelloParams);
        Assert.Equal(new HelloParams(1, "cli", "0.1.0", "stable"), hello);
    }

    [Fact]
    public void HelloResponse_EncodesAndDecodesCanonicalVector()
    {
        JsonElement data = JsonSerializer.SerializeToElement(
            new HelloResult(1, "0.1.0", 12345, "stable"), CoveJsonContext.Default.HelloResult);
        var response = new ControlResponse("1", true, data);

        byte[] encoded = ControlCodec.Encode(response);
        Assert.Equal(ProtocolVectors.HelloResponseJson, Encoding.UTF8.GetString(encoded));

        ControlResponse decoded = ControlCodec.DecodeResponse(encoded);
        Assert.Equal(response.Id, decoded.Id);
        Assert.True(decoded.Ok);
        Assert.Null(decoded.Error);
        var hello = decoded.Data!.Value.Deserialize(CoveJsonContext.Default.HelloResult);
        Assert.Equal(new HelloResult(1, "0.1.0", 12345, "stable"), hello);
    }

    [Fact]
    public void NookListRequest_EncodesAndDecodesCanonicalVector()
    {
        var request = new ControlRequest("1", "cove://commands/nook.list");

        byte[] encoded = ControlCodec.Encode(request);
        Assert.Equal(ProtocolVectors.NookListRequestJson, Encoding.UTF8.GetString(encoded));

        ControlRequest decoded = ControlCodec.DecodeRequest(encoded);
        Assert.Equal(request.Id, decoded.Id);
        Assert.Equal(request.Uri, decoded.Uri);
        Assert.Null(decoded.Params);
    }

    [Fact]
    public void SuccessResponse_EncodesAndDecodesCanonicalVector()
    {
        using var document = JsonDocument.Parse("{\"nooks\":[]}");
        var response = new ControlResponse("1", true, document.RootElement.Clone());

        byte[] encoded = ControlCodec.Encode(response);
        Assert.Equal(ProtocolVectors.NookListResponseJson, Encoding.UTF8.GetString(encoded));

        ControlResponse decoded = ControlCodec.DecodeResponse(encoded);
        Assert.Equal(response.Id, decoded.Id);
        Assert.True(decoded.Ok);
        Assert.Equal(0, decoded.Data!.Value.GetProperty("nooks").GetArrayLength());
        Assert.Null(decoded.Error);
    }

    [Fact]
    public void ErrorResponse_EncodesAndDecodesCanonicalVector()
    {
        var response = new ControlResponse(
            "1",
            false,
            null,
            new ControlError("not_found", "no nook 7f3a"));

        byte[] encoded = ControlCodec.Encode(response);
        Assert.Equal(ProtocolVectors.NotFoundResponseJson, Encoding.UTF8.GetString(encoded));

        ControlResponse decoded = ControlCodec.DecodeResponse(encoded);
        Assert.Equal(response.Id, decoded.Id);
        Assert.False(decoded.Ok);
        Assert.Null(decoded.Data);
        Assert.Equal(response.Error, decoded.Error);
    }
}
