using Xunit;

namespace Cove.Gui.Tests;

public sealed class ShellLaunchTests
{
    [Theory]
    [InlineData("/bin/zsh")]
    [InlineData("/bin/bash")]
    [InlineData("/usr/local/bin/fish")]
    public void UnixShells_RunInteractiveLoginCommands(string shell)
    {
        var (command, args) = ShellLaunch.Invocation(shell, "npm install -g pkg@latest");
        Assert.Equal(shell, command);
        Assert.Equal(new[] { "-ilc", "npm install -g pkg@latest" }, args);
    }

    [Theory]
    [InlineData("powershell.exe")]
    [InlineData("pwsh")]
    public void PowerShell_UsesCommandFlag(string shell)
    {
        var (command, args) = ShellLaunch.Invocation(shell, "npm install -g pkg@latest");
        Assert.Equal(shell, command);
        Assert.Equal(new[] { "-NoLogo", "-Command", "npm install -g pkg@latest" }, args);
    }

    [Fact]
    public void Cmd_UsesSlashC()
    {
        var (command, args) = ShellLaunch.Invocation("cmd.exe", "echo hi");
        Assert.Equal("cmd.exe", command);
        Assert.Equal(new[] { "/c", "echo hi" }, args);
    }
}
