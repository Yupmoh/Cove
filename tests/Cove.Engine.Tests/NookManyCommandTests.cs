using System.Text.Json;
using Cove.Engine.Layout;
using Cove.Protocol;
using NookLeaf = Cove.Persistence.NookLeaf;
using NookType = Cove.Persistence.NookType;
using SplitOrientation = Cove.Persistence.SplitOrientation;
using Subtab = Cove.Persistence.Subtab;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookManyCommandTests
{
    [Fact]
    public async Task OpenMany_ChainsMixedItemsAndBalancesOnce()
    {
        var requests = new List<ControlRequest>();
        var sequence = 0;
        var context = Context(new NookOpenManyParams(
            [
                new NookOpenManyItem("terminal", Command: "yazi"),
                new NookOpenManyItem("browser", Url: "https://example.com"),
                new NookOpenManyItem("agent", Adapter: "codex", Name: "Codex"),
            ],
            "nook-anchor",
            "right",
            "right"));
        context.Redrive = request =>
        {
            requests.Add(request);
            if (request.Uri == "cove://commands/nook.open")
            {
                var parameters = request.Params!.Value.Deserialize(CoveJsonContext.Default.NookOpenParams)!;
                var id = $"nook-{++sequence}";
                return Task.FromResult<ControlResponse?>(Ok(request, new NookOpenResult(
                    id,
                    parameters.NookType,
                    "bay-1",
                    "shore-1",
                    parameters.Placement),
                    CoveJsonContext.Default.NookOpenResult));
            }
            if (request.Uri == "cove://commands/agent.launch")
            {
                var parameters = request.Params!.Value.Deserialize(CoveJsonContext.Default.AgentLaunchParams)!;
                var id = $"nook-{++sequence}";
                return Task.FromResult<ControlResponse?>(Ok(request, new AgentLaunchResult(
                    id,
                    parameters.Adapter,
                    null,
                    "bay-1",
                    "shore-1",
                    parameters.Placement,
                    false),
                    CoveJsonContext.Default.AgentLaunchResult));
            }
            return Task.FromResult<ControlResponse?>(Ok(request, new NookStackResult(
                "nook-3",
                "bay-1",
                "shore-1",
                "right",
                3),
                CoveJsonContext.Default.NookStackResult));
        };

        var response = await NookManyCommands.OpenMany(context);

        Assert.True(response.Ok);
        Assert.Equal(
            ["cove://commands/nook.open", "cove://commands/nook.open", "cove://commands/agent.launch", "cove://commands/nook.stack"],
            requests.Select(request => request.Uri));
        Assert.Equal("nook-anchor", requests[0].Params!.Value.GetProperty("relativeToNookId").GetString());
        Assert.Equal("nook-1", requests[1].Params!.Value.GetProperty("relativeToNookId").GetString());
        Assert.Equal("nook-2", requests[2].Params!.Value.GetProperty("relativeToNookId").GetString());
        Assert.Equal("nook-3", requests[3].Params!.Value.GetProperty("nookId").GetString());
        var result = response.Data!.Value.Deserialize(CoveJsonContext.Default.NookOpenManyResult)!;
        Assert.Equal(["nook-1", "nook-2", "nook-3"], result.Opened.Select(item => item.NookId));
        Assert.NotNull(result.Balance);
    }

    [Fact]
    public async Task OpenMany_RollsBackCreatedNooksInReverseOrder()
    {
        var closed = new List<string>();
        var open = 0;
        var context = Context(new NookOpenManyParams(
            [
                new NookOpenManyItem("terminal", Command: "one"),
                new NookOpenManyItem("terminal", Command: "two"),
                new NookOpenManyItem("terminal", Command: "three"),
            ],
            "nook-anchor",
            "below"));
        context.Redrive = request =>
        {
            if (request.Uri == "cove://commands/nook.close")
            {
                var id = request.Params!.Value.GetProperty("nookId").GetString()!;
                closed.Add(id);
                return Task.FromResult<ControlResponse?>(Ok(request, new NookCloseResult(
                    id,
                    "terminal",
                    "bay-1",
                    "shore-1"),
                    CoveJsonContext.Default.NookCloseResult));
            }
            open++;
            if (open == 3)
                return Task.FromResult<ControlResponse?>(new ControlResponse(request.Id, false, null, new ControlError("launch_failed", "boom")));
            var nookId = $"nook-{open}";
            return Task.FromResult<ControlResponse?>(Ok(request, new NookOpenResult(
                nookId,
                "terminal",
                "bay-1",
                "shore-1",
                "below"),
                CoveJsonContext.Default.NookOpenResult));
        };

        var response = await NookManyCommands.OpenMany(context);

        Assert.False(response.Ok);
        Assert.Equal("launch_failed", response.Error!.Code);
        Assert.Equal(["nook-2", "nook-1"], closed);
    }

    [Fact]
    public async Task CloseOthers_SelectsRequestedBoundaryAndKeepsTarget()
    {
        var layout = new LayoutService();
        var shore = layout.CreateShore("Main", Leaf("keep"));
        layout.SplitNook(shore, "keep", SplitOrientation.Row, Leaf("same-shore"));
        var otherShore = layout.CreateShoreInWing("default", LayoutService.MainWingId, "Other", Leaf("same-bay"));
        Assert.NotEqual(shore, otherShore);
        var closed = new List<string>();
        var context = Context(new NookCloseOthersParams("keep", "same-bay"), layout);
        context.Redrive = request =>
        {
            var id = request.Params!.Value.GetProperty("nookId").GetString()!;
            closed.Add(id);
            var location = layout.ResolveNookLocation(id);
            return Task.FromResult<ControlResponse?>(Ok(request, new NookCloseResult(
                id,
                "terminal",
                location.BayId!,
                location.ShoreId!),
                CoveJsonContext.Default.NookCloseResult));
        };

        var response = await NookManyCommands.CloseOthers(context);

        Assert.True(response.Ok);
        Assert.Equal(["same-shore", "same-bay"], closed);
        Assert.DoesNotContain("keep", closed);
        var result = response.Data!.Value.Deserialize(CoveJsonContext.Default.NookCloseOthersResult)!;
        Assert.Equal("keep", result.KeptNookId);
        Assert.Equal(closed, result.Closed.Select(item => item.NookId));
    }

    private static EngineDispatchContext Context<T>(T parameters, LayoutService? layout = null)
    {
        var json = JsonSerializer.SerializeToElement(parameters, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)CoveJsonContext.Default.GetTypeInfo(typeof(T))!);
        return new EngineDispatchContext(new ControlRequest("request-1", "test", json), layout: layout);
    }

    private static ControlResponse Ok<T>(
        ControlRequest request,
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        new(request.Id, true, JsonSerializer.SerializeToElement(value, typeInfo));

    private static NookLeaf Leaf(string nookId) => new()
    {
        NookId = nookId,
        Subtabs = [new Subtab(nookId, NookType.Terminal)],
    };
}
