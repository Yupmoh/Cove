using System.Text;
using System.Text.Json;
using Xunit;

namespace Cove.Protocol.Tests;

public sealed class ControlCodecTests
{
    [Fact]
    public void HelloRequest_SerializesExact()
    {
        JsonElement p = JsonSerializer.SerializeToElement(
            new HelloParams(1, "cli", "0.1.0", "stable"), CoveJsonContext.Default.HelloParams);
        var req = new ControlRequest("1", "cove://sys/hello", p);
        string json = Encoding.UTF8.GetString(ControlCodec.Encode(req));
        Assert.Equal(
            "{\"id\":\"1\",\"uri\":\"cove://sys/hello\",\"params\":{\"protocolVersion\":1,\"clientKind\":\"cli\",\"clientVersion\":\"0.1.0\",\"channel\":\"stable\"}}",
            json);
    }

    [Fact]
    public void HelloResponse_SerializesExact()
    {
        JsonElement d = JsonSerializer.SerializeToElement(
            new HelloResult(1, "0.1.0", 12345, "stable"), CoveJsonContext.Default.HelloResult);
        var resp = new ControlResponse("1", true, d);
        string json = Encoding.UTF8.GetString(ControlCodec.Encode(resp));
        Assert.Equal(
            "{\"id\":\"1\",\"ok\":true,\"data\":{\"protocolVersion\":1,\"engineVersion\":\"0.1.0\",\"enginePid\":12345,\"channel\":\"stable\"}}",
            json);
    }

    [Fact]
    public void NookListRequest_OmitsNullFields()
    {
        var req = new ControlRequest("1", "cove://commands/nook.list");
        string json = Encoding.UTF8.GetString(ControlCodec.Encode(req));
        Assert.Equal("{\"id\":\"1\",\"uri\":\"cove://commands/nook.list\"}", json);
    }

    [Fact]
    public void SuccessResponse_OmitsError()
    {
        JsonElement d = JsonDocument.Parse("{\"nooks\":[]}").RootElement.Clone();
        var resp = new ControlResponse("1", true, d);
        string json = Encoding.UTF8.GetString(ControlCodec.Encode(resp));
        Assert.Equal("{\"id\":\"1\",\"ok\":true,\"data\":{\"nooks\":[]}}", json);
    }

    [Fact]
    public void ErrorResponse_OmitsData()
    {
        var resp = new ControlResponse("1", false, null, new ControlError("not_found", "no nook 7f3a"));
        string json = Encoding.UTF8.GetString(ControlCodec.Encode(resp));
        Assert.Equal("{\"id\":\"1\",\"ok\":false,\"error\":{\"code\":\"not_found\",\"message\":\"no nook 7f3a\"}}", json);
    }
}
