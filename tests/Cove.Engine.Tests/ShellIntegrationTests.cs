using System.Collections.Generic;
using System.IO;
using Cove.Engine;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ShellIntegrationTests
{
    [Fact]
    public void Install_WritesScripts()
    {
        var dir = Path.Combine(Path.GetTempPath(), "coveshell-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var shellDir = ShellIntegration.Install(dir);

            Assert.True(File.Exists(Path.Combine(shellDir, "zsh", ".zshrc")));
            Assert.True(File.Exists(Path.Combine(shellDir, "cove.bashrc")));

            var zshrc = File.ReadAllText(Path.Combine(shellDir, "zsh", ".zshrc"));
            Assert.Contains("_cove_osc7", zshrc);
            Assert.Contains("printf", zshrc);
            Assert.Contains("file://", zshrc);

            var bashrc = File.ReadAllText(Path.Combine(shellDir, "cove.bashrc"));
            Assert.Contains("_cove_osc7", bashrc);
            Assert.Contains("printf", bashrc);
            Assert.Contains("file://", bashrc);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void Apply_Zsh_SetsZdotdir()
    {
        var env = new Dictionary<string, string> { ["HOME"] = "/home/u" };
        var args = ShellIntegration.Apply("/bin/zsh", "/shell", System.Array.Empty<string>(), env);

        Assert.Equal(Path.Combine("/shell", "zsh"), env["ZDOTDIR"]);
        Assert.Equal("/home/u", env["COVE_ZDOTDIR_ORIG"]);
        Assert.Empty(args);
    }

    [Fact]
    public void Apply_Zsh_PreservesExistingZdotdirAsOrig()
    {
        var env = new Dictionary<string, string> { ["ZDOTDIR"] = "/custom" };
        var args = ShellIntegration.Apply("/bin/zsh", "/shell", System.Array.Empty<string>(), env);

        Assert.Equal("/custom", env["COVE_ZDOTDIR_ORIG"]);
        Assert.Equal(Path.Combine("/shell", "zsh"), env["ZDOTDIR"]);
    }

    [Fact]
    public void Apply_Bash_AddsRcfile()
    {
        var args = ShellIntegration.Apply("/bin/bash", "/shell", System.Array.Empty<string>(), new Dictionary<string, string>());

        Assert.Contains("--rcfile", args);
        Assert.Contains(Path.Combine("/shell", "cove.bashrc"), args);
    }

    [Fact]
    public void Apply_OtherShell_Unchanged()
    {
        var env = new Dictionary<string, string>();
        var args = ShellIntegration.Apply("/usr/bin/fish", "/shell", System.Array.Empty<string>(), env);

        Assert.Empty(args);
        Assert.False(env.ContainsKey("ZDOTDIR"));
    }
}
