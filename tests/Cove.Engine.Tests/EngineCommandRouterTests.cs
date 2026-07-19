using System.Threading.Tasks;
using Cove.Engine;
using Cove.Protocol;
using Xunit;

public class EngineCommandRouterTests
{
    [Fact]
    public async Task NookList_ReturnsEmptyNooks()
    {
        var request = new ControlRequest("1", "cove://commands/nook.list");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Equal("{\"nooks\":[]}", response.Data!.Value.GetRawText());
    }

    [Fact]
    public async Task UnknownUri_ReturnsNull()
    {
        var request = new ControlRequest("1", "cove://commands/does.not.exist");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.Null(response);
    }

    [Fact]
    public async Task WindowFocus_UsesGeneratedDispatchContract()
    {
        var request = new ControlRequest(
            "1",
            "cove://commands/window.focus");
        var response = await EngineCommandRouter.RouteAsync(
            request,
            forwardWindowFocus: _ => false);

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Equal(
            """
            {"focused":false,"reason":"no_render_client"}
            """,
            response.Data!.Value.GetRawText());
    }

    [Fact]
    public async Task RestoreSummary_UsesGeneratedDispatchContract()
    {
        var request = new ControlRequest(
            "1",
            "cove://commands/restore.summary.get");
        var response = await EngineCommandRouter.RouteAsync(
            request,
            getRestorationSummary: () =>
                new Cove.Engine.Restart.RestorationSummaryEvent(
                    3,
                    2,
                    1,
                    "2026-07-18T00:00:00.0000000+00:00"));

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        var result = response.Data!.Value;
        Assert.True(result.GetProperty("present").GetBoolean());
        Assert.Equal(3, result.GetProperty("restored").GetInt32());
        Assert.Equal(2, result.GetProperty("fresh").GetInt32());
        Assert.Equal(1, result.GetProperty("skipped").GetInt32());
        Assert.Equal(
            "2026-07-18T00:00:00.0000000+00:00",
            result.GetProperty("bootedAt").GetString());
    }
}
