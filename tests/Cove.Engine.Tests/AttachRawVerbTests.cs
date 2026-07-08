using System.Text.Json;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AttachRawVerbTests
{
    [Fact]
    public async Task AttachRaw_VerbDispatchesAndReturnsNotImplemented()
    {
        var prm = JsonDocument.Parse("{\"session\":\"test-session-123\"}").RootElement.Clone();
        var request = new ControlRequest("1", "cove://commands/attach.raw", prm);
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("not_implemented", response.Error?.Code);
    }

    [Fact]
    public async Task AttachRaw_MissingParamsReturnsInvalidParams()
    {
        var request = new ControlRequest("1", "cove://commands/attach.raw");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("invalid_params", response.Error?.Code);
    }
}
