using System.Text.Json;
using Cove.Engine;
using Cove.Engine.Hooks;
using Cove.Engine.Protocol;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HookCommandTests
{
    [Fact]
    public async Task HookEmit_UsesGeneratedRouteAndUpdatesHookState()
    {
        var hookRouter = new HookEventRouter();
        using var payloadDocument = JsonDocument.Parse("""{"session_id":"session-hook-1","nested":{"value":7}}""");
        var parameters = new HookEmitParams(
            Adapter: "claude-code",
            Event: "session-start",
            NookId: "nook-hook-1",
            Payload: payloadDocument.RootElement.Clone());
        var request = new ControlRequest(
            "hook-1",
            "cove://commands/hook.emit",
            JsonSerializer.SerializeToElement(parameters, CoveJsonContext.Default.HookEmitParams));

        var response = await EngineCommandRouter.RouteAsync(request, hookRouter: hookRouter);

        Assert.NotNull(response);
        Assert.True(response!.Ok, response.Error?.Message);
        Assert.Equal("{}", response.Data!.Value.GetRawText());
        var state = hookRouter.GetNookState("nook-hook-1");
        Assert.NotNull(state);
        Assert.Equal("claude-code", state!.Adapter);
        Assert.Equal("idle", state.Status);
        Assert.Equal("session-hook-1", state.SessionId);
    }

    [Theory]
    [InlineData("{\"event\":\"session-start\",\"nookId\":\"nook-required\",\"payload\":{}}")]
    [InlineData("{\"adapter\":\"claude-code\",\"nookId\":\"nook-required\",\"payload\":{}}")]
    [InlineData("{\"adapter\":\"\",\"event\":\"session-start\",\"nookId\":\"nook-required\",\"payload\":{}}")]
    [InlineData("{\"adapter\":\"claude-code\",\"event\":\"\",\"nookId\":\"nook-required\",\"payload\":{}}")]
    public async Task HookEmit_RequiresAdapterAndEvent(string paramsJson)
    {
        var hookRouter = new HookEventRouter();
        using var document = JsonDocument.Parse(paramsJson);
        var request = new ControlRequest(
            "hook-required",
            "cove://commands/hook.emit",
            document.RootElement.Clone());

        var response = await EngineCommandRouter.RouteAsync(request, hookRouter: hookRouter);

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("invalid_params", response.Error?.Code);
        Assert.Empty(hookRouter.GetAllNookStates());
    }

    [Fact]
    public async Task HookEmit_AttributedNookCanUpdateItsOwnHookState()
    {
        var hookRouter = new HookEventRouter();
        using var payloadDocument = JsonDocument.Parse("{}");
        var parameters = new HookEmitParams(
            Adapter: "claude-code",
            Event: "session-start",
            NookId: "nook-hook-own",
            Payload: payloadDocument.RootElement.Clone());
        var request = new ControlRequest(
            "hook-own",
            "cove://commands/hook.emit",
            JsonSerializer.SerializeToElement(parameters, CoveJsonContext.Default.HookEmitParams),
            CallerNookId: "nook-hook-own");
        var scopeStore = new NookScopeStore(
            Path.Combine(Path.GetTempPath(), $"cove-hook-scope-{Guid.NewGuid():N}"));

        var response = await EngineCommandRouter.RouteAsync(
            request,
            hookRouter: hookRouter,
            nookScopes: scopeStore);

        Assert.NotNull(response);
        Assert.True(response!.Ok, response.Error?.Message);
        Assert.NotNull(hookRouter.GetNookState("nook-hook-own"));
    }

    [Fact]
    public async Task HookEmit_AttributedNookCannotUpdateAnotherNook()
    {
        var hookRouter = new HookEventRouter();
        using var payloadDocument = JsonDocument.Parse("{}");
        var parameters = new HookEmitParams(
            Adapter: "claude-code",
            Event: "session-start",
            NookId: "nook-hook-target",
            Payload: payloadDocument.RootElement.Clone());
        var request = new ControlRequest(
            "hook-cross-nook",
            "cove://commands/hook.emit",
            JsonSerializer.SerializeToElement(parameters, CoveJsonContext.Default.HookEmitParams),
            CallerNookId: "nook-hook-caller");
        var scopeStore = new NookScopeStore(
            Path.Combine(Path.GetTempPath(), $"cove-hook-scope-{Guid.NewGuid():N}"));

        var response = await EngineCommandRouter.RouteAsync(
            request,
            hookRouter: hookRouter,
            nookScopes: scopeStore);

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("access_denied", response.Error?.Code);
        Assert.Empty(hookRouter.GetAllNookStates());
    }
}
