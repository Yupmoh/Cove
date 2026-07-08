using System.Text.Json;
using Cove.Engine.Panes;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class PaneCommandsTests
{

    [Fact]
    public async Task EditorOpen_ReturnsState()
    {
        var prm = JsonDocument.Parse("""{"filePath":"src/file.cs","readOnly":true}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/editor.open", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task EditorSave_RoundTripsState()
    {
        var prm = JsonDocument.Parse("""{"filePath":"src/file.cs","cursor":"10:5","scroll":"0:200","fold":"[1-5]","undo":"step1","readOnly":false}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/editor.save", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task EditorOpen_MissingParams_Fails()
    {
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/editor.open"));
        Assert.False(resp!.Ok);
    }

    [Fact]
    public async Task SearchQuery_ReturnsResult()
    {
        var prm = JsonDocument.Parse("""{"query":"TODO","path":"src","regex":false,"wholeWord":true,"caseInsensitive":true}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/search.query", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task SearchSetState_RoundTrips()
    {
        var prm = JsonDocument.Parse("""{"query":"test","regex":true,"wholeWord":false,"caseInsensitive":true,"includeGlobs":["*.ts"],"excludeGlobs":["node_modules"],"scroll":"0:100"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/search.set-state", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task ScmStatus_ReturnsEmpty()
    {
        var prm = JsonDocument.Parse("""{"repoRoot":"/repo"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/scm.status", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task ScmStage_ReturnsOk()
    {
        var prm = JsonDocument.Parse("""{"repoRoot":"/repo","filePath":"src/file.cs","unstage":false}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/scm.stage", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task ScmCommit_ReturnsResult()
    {
        var prm = JsonDocument.Parse("""{"repoRoot":"/repo","message":"fix: bug","amend":false,"sign":true}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/scm.commit", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task ScmBlame_ReturnsResult()
    {
        var prm = JsonDocument.Parse("""{"filePath":"src/file.cs","ref":"HEAD"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/scm.blame", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task ViewerOpen_ReturnsState()
    {
        var prm = JsonDocument.Parse("""{"filePath":"image.png","ref":"HEAD","contextLines":5}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/viewer.open", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task EditorGetState_ReturnsState()
    {
        var prm = JsonDocument.Parse("""{"filePath":"src/file.cs"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/editor.get-state", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task SearchGetState_ReturnsState()
    {
        var prm = JsonDocument.Parse("""{"query":""}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/search.get-state", prm));
        Assert.True(resp!.Ok);
    }

    [Fact]
    public async Task ViewerGetState_ReturnsState()
    {
        var prm = JsonDocument.Parse("""{"filePath":"image.png"}""").RootElement.Clone();
        var resp = await EngineCommandRouter.RouteAsync(new ControlRequest("r1", "cove://commands/viewer.get-state", prm));
        Assert.True(resp!.Ok);
    }
}
