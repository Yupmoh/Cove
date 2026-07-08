using Cove.Engine.Panes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class PaneTitleDeriverTests
{
    [Fact]
    public void DeriveTitle_CustomTitle_TakesPrecedence()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var title = deriver.DeriveTitle("claude", "/bin/bash", "/home/user", "My Pane");
        Assert.Equal("My Pane", title);
    }

    [Fact]
    public void DeriveTitle_AdapterName()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var title = deriver.DeriveTitle("claude", null, null, null);
        Assert.Equal("claude", title);
    }

    [Fact]
    public void DeriveTitle_AdapterWithPath_StripsPath()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var title = deriver.DeriveTitle("adapters/claude", null, null, null);
        Assert.Equal("claude", title);
    }

    [Fact]
    public void DeriveTitle_AdapterWithVersion_StripsVersion()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var title = deriver.DeriveTitle("claude:latest", null, null, null);
        Assert.Equal("claude", title);
    }

    [Fact]
    public void DeriveTitle_Command_FallsBackWhenNoAdapter()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var title = deriver.DeriveTitle(null, "/bin/bash", null, null);
        Assert.Equal("bash", title);
    }

    [Fact]
    public void DeriveTitle_PythonScript_UsesScriptName()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var title = deriver.DeriveTitle(null, "python /home/user/app.py", null, null);
        Assert.Equal("app.py", title);
    }

    [Fact]
    public void DeriveTitle_NodeScript_UsesScriptName()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var title = deriver.DeriveTitle(null, "node /home/user/server.js", null, null);
        Assert.Equal("server.js", title);
    }

    [Fact]
    public void DeriveTitle_Directory_FallsBackWhenNoCommand()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var title = deriver.DeriveTitle(null, null, "/home/user/myproject", null);
        Assert.Equal("myproject", title);
    }

    [Fact]
    public void DeriveTitle_AllNull_ReturnsDefault()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var title = deriver.DeriveTitle(null, null, null, null);
        Assert.Equal("terminal", title);
    }

    [Fact]
    public void DeriveTitle_LongAdapter_Truncates()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var longName = new string('a', 50);
        var title = deriver.DeriveTitle(longName, null, null, null);
        Assert.Equal(32, title.Length);
        Assert.EndsWith("...", title);
    }

    [Fact]
    public void DeriveTitle_LongCommand_Truncates()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var longCmd = new string('x', 50);
        var title = deriver.DeriveTitle(null, longCmd, null, null);
        Assert.Equal(32, title.Length);
        Assert.EndsWith("...", title);
    }

    [Fact]
    public void DeriveTitle_LongDirectory_Truncates()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var longDir = "/home/" + new string('d', 50);
        var title = deriver.DeriveTitle(null, null, longDir, null);
        Assert.Equal(32, title.Length);
        Assert.EndsWith("...", title);
    }

    [Fact]
    public void DeriveTitle_EmptyCustomTitle_FallsThrough()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var title = deriver.DeriveTitle("claude", null, null, "  ");
        Assert.Equal("claude", title);
    }

    [Fact]
    public void DeriveTitle_RootDirectory_ReturnsRoot()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var title = deriver.DeriveTitle(null, null, "/", null);
        Assert.Equal("/", title);
    }

    [Fact]
    public void DeriveTitle_WindowsPath()
    {
        var deriver = new PaneTitleDeriver(NullLogger.Instance);
        var title = deriver.DeriveTitle(null, null, "C:\\Users\\dev\\project", null);
        Assert.Equal("project", title);
    }
}
