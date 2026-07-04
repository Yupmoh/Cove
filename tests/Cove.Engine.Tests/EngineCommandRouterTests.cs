using System.Threading.Tasks;
using Cove.Engine;
using Cove.Protocol;
using Xunit;

public class EngineCommandRouterTests
{
    [Fact]
    public async Task PaneList_ReturnsEmptyPanes()
    {
        var request = new ControlRequest("1", "cove://commands/pane.list");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Equal("{\"panes\":[]}", response.Data!.Value.GetRawText());
    }

    [Fact]
    public async Task UnknownUri_ReturnsNull()
    {
        var request = new ControlRequest("1", "cove://commands/does.not.exist");
        var response = await EngineCommandRouter.RouteAsync(request);
        Assert.Null(response);
    }
}
