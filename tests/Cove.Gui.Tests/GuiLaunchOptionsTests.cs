using Cove.Gui;
using Xunit;

namespace Cove.Gui.Tests;

public sealed class GuiLaunchOptionsTests
{
    [Theory]
    [InlineData(null, "dev", 7420)]
    [InlineData("", "stable", 0)]
    [InlineData(" ", "stable", 0)]
    [InlineData(null, "preview", 7420)]
    public void ResolveLoopbackPort_DefaultsByChannel(string? value, string channel, int expected)
    {
        Assert.Equal(expected, GuiLaunchOptions.ResolveLoopbackPort(value, channel));
    }

    [Theory]
    [InlineData("17420", "dev", 17420)]
    [InlineData("17420", "stable", 17420)]
    [InlineData("65535", "stable", 65535)]
    public void ResolveLoopbackPort_AcceptsExplicitPrivatePort(string value, string channel, int expected)
    {
        Assert.Equal(expected, GuiLaunchOptions.ResolveLoopbackPort(value, channel));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1023")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    [InlineData("17420.0")]
    public void ResolveLoopbackPort_RejectsInvalidOrPrivilegedPort(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => GuiLaunchOptions.ResolveLoopbackPort(value, "stable"));

        Assert.Contains("COVE_GUI_PORT", error.Message);
        Assert.Contains(value, error.Message);
    }
}
