using System;
using Cove.Engine.Pty;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CwdResolveTests
{
    [Fact]
    public void Resolve_InheritedWins()
    {
        Assert.Equal("/proj/src", PaneRegistry.ResolveWorkingDirectory("/proj/src", "/other"));
    }

    [Fact]
    public void Resolve_EmptyInherited_FallsToExplicit()
    {
        Assert.Equal("/other", PaneRegistry.ResolveWorkingDirectory(null, "/other"));
        Assert.Equal("/other", PaneRegistry.ResolveWorkingDirectory("", "/other"));
    }

    [Fact]
    public void Resolve_NeitherGiven_UsesHome()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(home, PaneRegistry.ResolveWorkingDirectory(null, null));
        Assert.Equal(home, PaneRegistry.ResolveWorkingDirectory("", ""));
    }
}
