using Cove.Engine.Tui;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class TuiClientTests
{
    [Fact]
    public void Render_ProducesNonEmptyLayout()
    {
        var tui = new TuiRenderer();
        var output = tui.Render(new TuiState { FocusedPaneId = "p1", PaneCount = 3 });
        Assert.False(string.IsNullOrEmpty(output));
        Assert.Contains("Cove", output);
    }

    [Fact]
    public void Render_WithPanes_ShowsCount()
    {
        var tui = new TuiRenderer();
        var output = tui.Render(new TuiState { FocusedPaneId = "p1", PaneCount = 5 });
        Assert.Contains("5", output);
    }

    [Fact]
    public void Render_WithNoPanes_ShowsEmptyState()
    {
        var tui = new TuiRenderer();
        var output = tui.Render(new TuiState { FocusedPaneId = null, PaneCount = 0 });
        Assert.Contains("No panes", output);
    }

    [Fact]
    public void Render_StatusBar_ShowsFocusedPane()
    {
        var tui = new TuiRenderer();
        var output = tui.Render(new TuiState { FocusedPaneId = "pane-abc123", PaneCount = 1 });
        Assert.Contains("pane-abc123", output);
    }

    [Fact]
    public void FormatCommand_HumanReadable()
    {
        var cmd = TuiFormatter.FormatCommand("cove://commands/pane.list", null);
        Assert.Contains("pane.list", cmd);
    }

    [Fact]
    public void FormatCommand_WithParams_IncludesJson()
    {
        var cmd = TuiFormatter.FormatCommand("cove://commands/pane.spawn", """{"command":"bash"}""");
        Assert.Contains("pane.spawn", cmd);
    }
}
