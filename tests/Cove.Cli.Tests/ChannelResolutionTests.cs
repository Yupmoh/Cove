using System;
using Cove.Cli;
using Cove.Platform;
using Xunit;

namespace Cove.Cli.Tests;

public sealed class ChannelResolutionTests
{
    [Fact]
    public void Flag_Wins_Over_Env()
    {
        Assert.Equal(CoveChannel.Dev, CliChannel.Resolve(new[] { "--channel", "dev" }, "beta", null));
    }

    [Fact]
    public void Flag_Equals_Syntax_Parsed()
    {
        Assert.Equal(CoveChannel.Beta, CliChannel.Resolve(new[] { "--channel=beta" }, null, null));
    }

    [Fact]
    public void Env_Honored_When_No_Flag()
    {
        Assert.Equal(CoveChannel.Dev, CliChannel.Resolve(Array.Empty<string>(), "dev", null));
    }

    [Fact]
    public void Env_Case_Insensitive()
    {
        Assert.Equal(CoveChannel.Dev, CliChannel.Resolve(Array.Empty<string>(), "DEV", null));
    }

    [Fact]
    public void Default_Is_Stable_When_Nothing_Set()
    {
        Assert.Equal(CoveChannel.Stable, CliChannel.Resolve(Array.Empty<string>(), null, null));
    }

    [Fact]
    public void Invalid_Env_Falls_Back_To_Stable()
    {
        Assert.Equal(CoveChannel.Stable, CliChannel.Resolve(Array.Empty<string>(), "bogus", null));
    }

    [Fact]
    public void Invalid_Flag_Falls_Back_To_Stable()
    {
        Assert.Equal(CoveChannel.Stable, CliChannel.Resolve(new[] { "--channel", "bogus" }, "dev", null));
    }
}
