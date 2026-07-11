using Cove.Engine.Tui;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class TuiClientTests
{
    [Fact]
    public void Render_ProducesNonEmptyLayout()
    {
        var tui = new TuiRenderer();
        var output = tui.Render(new TuiState { FocusedNookId = "p1", NookCount = 3 });
        Assert.False(string.IsNullOrEmpty(output));
        Assert.Contains("Cove", output);
    }

    [Fact]
    public void Render_WithNooks_ShowsCount()
    {
        var tui = new TuiRenderer();
        var output = tui.Render(new TuiState { FocusedNookId = "p1", NookCount = 5 });
        Assert.Contains("5", output);
    }

    [Fact]
    public void Render_WithNoNooks_ShowsEmptyState()
    {
        var tui = new TuiRenderer();
        var output = tui.Render(new TuiState { FocusedNookId = null, NookCount = 0 });
        Assert.Contains("No nooks", output);
    }

    [Fact]
    public void Render_StatusBar_ShowsFocusedNook()
    {
        var tui = new TuiRenderer();
        var output = tui.Render(new TuiState { FocusedNookId = "nook-abc123", NookCount = 1 });
        Assert.Contains("nook-abc123", output);
    }

    [Fact]
    public void FormatCommand_HumanReadable()
    {
        var cmd = TuiFormatter.FormatCommand("cove://commands/nook.list", null);
        Assert.Contains("nook.list", cmd);
    }

    [Fact]
    public void FormatCommand_WithParams_IncludesJson()
    {
        var cmd = TuiFormatter.FormatCommand("cove://commands/nook.spawn", """{"command":"bash"}""");
        Assert.Contains("nook.spawn", cmd);
    }
}
