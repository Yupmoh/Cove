using Cove.Gui;
using Xunit;

namespace Cove.Gui.Tests;

public sealed class GuiLaunchOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ResolveLoopbackPort_DefaultsToNormalGuiPort(string? value)
    {
        Assert.Equal(LoopbackServer.DefaultPort, GuiLaunchOptions.ResolveLoopbackPort(value));
    }

    [Theory]
    [InlineData("17420", 17420)]
    [InlineData("65535", 65535)]
    public void ResolveLoopbackPort_AcceptsExplicitPrivatePort(string value, int expected)
    {
        Assert.Equal(expected, GuiLaunchOptions.ResolveLoopbackPort(value));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1023")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    [InlineData("17420.0")]
    public void ResolveLoopbackPort_RejectsInvalidOrPrivilegedPort(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => GuiLaunchOptions.ResolveLoopbackPort(value));

        Assert.Contains("COVE_GUI_PORT", error.Message);
        Assert.Contains(value, error.Message);
    }
}
